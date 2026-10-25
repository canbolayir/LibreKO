using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class AdminPlayerEditorTests
{
    private const byte AckResult = 0x11;
    private const int Ruby = 389010000;
    private const int Sword = 120010000;
    private const int OfflineId = 7;
    private const byte FirstBagSlot = InventoryConstants.InventoryStart;

    private sealed record Sheet(int Id, string Name, bool Online, byte Level, int Gold, uint LastSeen, List<(byte Position, int ItemId, ushort Count)> Slots);

    private sealed class Fixture
    {
        public required AdminPlayerEditor Editor { get; init; }
        public required UserSession Gm { get; init; }
        public required UserSession Target { get; init; }
        public required List<Packet> ToGm { get; init; }
        public required List<Packet> ToTarget { get; init; }
        public required IItemGrantService Grants { get; init; }
    }

    private static (UserSession Session, List<Packet> Sent) Join(SessionManager sessions, int id, string name)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(packet => sent.Add(packet)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, id, id);
        session.Name = name;
        session.Level = 70;
        return (session, sent);
    }

    private static Fixture Arrange()
    {
        var sessions = new SessionManager();
        var (gm, toGm) = Join(sessions, 1, "Zeus");
        gm.IsGM = true;
        var (target, toTarget) = Join(sessions, 2, "Rin");
        target.Money = 5_000;

        var gameData = Substitute.For<IGameDataService>();
        gameData.GetItem(Ruby).Returns(new ItemData { Num = Ruby, Countable = 1, Duration = 1 });
        gameData.GetItem(Sword).Returns(new ItemData { Num = Sword, Countable = 0, Duration = 5000 });

        var stored = new ItemSlot[InventoryConstants.InventoryTotal];
        for (var index = 0; index < stored.Length; index++) stored[index] = new ItemSlot();
        stored[FirstBagSlot] = new ItemSlot { ItemId = Ruby, Count = 3 };
        var directory = Substitute.For<ICharacterDirectory>();
        directory.SheetAsync(OfflineId).Returns(new StoredCharacterSheet(
            new StoredCharacter(OfflineId, "Rias", 61, 207, 21, 812, 433, new DateTime(2026, 10, 16, 20, 0, 0, DateTimeKind.Utc)),
            1_200, 300, 50, stored));

        var grants = Substitute.For<IItemGrantService>();
        var editor = new AdminPlayerEditor(sessions, directory, gameData, grants,
            Substitute.For<IUserNotificationService>(), Substitute.For<ILogger<AdminPlayerEditor>>());
        return new Fixture { Editor = editor, Gm = gm, Target = target, ToGm = toGm, ToTarget = toTarget, Grants = grants };
    }

    private static Packet SheetRequest(int characterId)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteInt(characterId);
        packet.ResetOffset();
        return packet;
    }

    private static Packet EditRequest(int characterId, byte action, byte position, int itemId, ushort count)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteInt(characterId);
        packet.WriteByte(action);
        packet.WriteByte(position);
        packet.WriteInt(itemId);
        packet.WriteUShort(count);
        packet.ResetOffset();
        return packet;
    }

    private static Sheet ReadSheet(List<Packet> sent)
    {
        var packet = sent.Last(p => { p.ResetOffset(); return p.ReadByte() == AdminPlayerEditor.AckPlayerSheet; });
        packet.ResetOffset();
        packet.ReadByte();
        var id = packet.ReadInt();
        var name = packet.ReadSByteString();
        var online = packet.ReadByte() == AdminPanelPacketWriter.Granted;
        var level = packet.ReadByte();
        packet.ReadShort();
        packet.ReadByte();
        packet.ReadByte();
        packet.ReadUShort();
        packet.ReadUShort();
        var gold = packet.ReadInt();
        packet.ReadInt();
        packet.ReadInt();
        var lastSeen = packet.ReadUInt();
        int count = packet.ReadByte();
        var slots = new List<(byte, int, ushort)>();
        for (var i = 0; i < count; i++)
        {
            var position = packet.ReadByte();
            var itemId = packet.ReadInt();
            var itemCount = packet.ReadUShort();
            packet.ReadShort();
            packet.ReadByte();
            slots.Add((position, itemId, itemCount));
        }

        return new Sheet(id, name, online, level, gold, lastSeen, slots);
    }

    private static (bool Ok, string Message) ReadResult(List<Packet> sent)
    {
        var packet = sent.Last(p => { p.ResetOffset(); return p.ReadByte() == AckResult; });
        packet.ResetOffset();
        packet.ReadByte();
        return (packet.ReadByte() == AdminPanelPacketWriter.Granted, packet.ReadString());
    }

    [Fact]
    public async Task AnOnlinePlayersSheetCarriesTheirGearAndBag()
    {
        var f = Arrange();
        f.Target.Inventory[InventoryConstants.Head] = new ItemSlot { ItemId = Sword, Count = 1, Durability = 5000 };
        f.Target.Inventory[FirstBagSlot + 2] = new ItemSlot { ItemId = Ruby, Count = 9 };

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqPlayerSheet, SheetRequest(2));

        var sheet = ReadSheet(f.ToGm);
        sheet.Should().BeEquivalentTo(new Sheet(2, "Rin", true, 70, 5_000, 0,
            [((byte)InventoryConstants.Head, Sword, (ushort)1), ((byte)(FirstBagSlot + 2), Ruby, (ushort)9)]));
    }

    [Fact]
    public async Task AnOfflineCharacterIsReadFromTheDatabaseWithWhenTheyWereLastSeen()
    {
        var f = Arrange();

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqPlayerSheet, SheetRequest(OfflineId));

        var sheet = ReadSheet(f.ToGm);
        sheet.Online.Should().BeFalse();
        sheet.Name.Should().Be("Rias");
        sheet.LastSeen.Should().BeGreaterThan(0);
        sheet.Slots.Should().Equal((FirstBagSlot, Ruby, (ushort)3));
    }

    [Fact]
    public async Task PuttingAnItemFillsTheBagSlotAndRefreshesTheOwnersBag()
    {
        var f = Arrange();

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditPut, FirstBagSlot + 1, Ruby, 50));

        f.Target.Inventory[FirstBagSlot + 1].ItemId.Should().Be(Ruby);
        f.Target.Inventory[FirstBagSlot + 1].Count.Should().Be(50);
        f.ToTarget.Should().NotBeEmpty();
        ReadSheet(f.ToGm).Slots.Should().Contain((FirstBagSlot + 1, Ruby, 50));
    }

    [Fact]
    public async Task AnItemThatDoesNotStackIsPutOneAtATimeWithItsDurability()
    {
        var f = Arrange();

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditPut, FirstBagSlot, Sword, 20));

        f.Target.Inventory[FirstBagSlot].Count.Should().Be(1);
        f.Target.Inventory[FirstBagSlot].Durability.Should().Be(5000);
    }

    [Fact]
    public async Task DeletingEmptiesTheSlot()
    {
        var f = Arrange();
        f.Target.Inventory[FirstBagSlot] = new ItemSlot { ItemId = Ruby, Count = 4 };

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditDelete, FirstBagSlot, 0, 0));

        f.Target.Inventory[FirstBagSlot].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task GearSlotsAndLinkedItemsAreLeftAlone()
    {
        var f = Arrange();
        f.Target.Inventory[InventoryConstants.Head] = new ItemSlot { ItemId = Sword, Count = 1 };
        f.Target.Inventory[FirstBagSlot] = new ItemSlot { ItemId = Ruby, Count = 1, UniqueId = 77 };

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditDelete, InventoryConstants.Head, 0, 0));
        ReadResult(f.ToGm).Ok.Should().BeFalse();
        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditDelete, FirstBagSlot, 0, 0));
        ReadResult(f.ToGm).Ok.Should().BeFalse();

        f.Target.Inventory[InventoryConstants.Head].ItemId.Should().Be(Sword);
        f.Target.Inventory[FirstBagSlot].ItemId.Should().Be(Ruby);
        f.ToTarget.Should().BeEmpty();
    }

    [Fact]
    public async Task AnOfflineCharactersBagIsNotChanged()
    {
        var f = Arrange();

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(OfflineId, AdminPlayerEditor.EditPut, FirstBagSlot, Ruby, 1));

        ReadResult(f.ToGm).Ok.Should().BeFalse();
    }

    [Fact]
    public async Task GivingGoesThroughTheItemGrantAndFailsWhenTheBagIsFull()
    {
        var f = Arrange();
        f.Grants.GrantAsync(f.Target, Arg.Any<ItemData>(), 5).Returns(5, 0);

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditGive, 0, Ruby, 5));
        ReadSheet(f.ToGm).Name.Should().Be("Rin");

        await f.Editor.HandleAsync(f.Gm, AdminPlayerEditor.ReqInventoryEdit, EditRequest(2, AdminPlayerEditor.EditGive, 0, Ruby, 5));
        ReadResult(f.ToGm).Ok.Should().BeFalse();
    }
}
