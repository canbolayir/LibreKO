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

public class AdminMonsterEditorTests
{
    private const byte AckResult = 0x11;
    private const int WormId = 750;
    private const short WormDropRow = 40;
    private const int Gold = 1000;
    private const int Ruby = 389010000;
    private const int Topaz = 389020000;
    private const int Group = 9001;
    private const byte GoldField = 9;
    private const byte LevelField = 1;

    private sealed class Fixture
    {
        public readonly SessionManager Sessions = new();
        public readonly List<Packet> Sent = [];
        public readonly IClient Client = Substitute.For<IClient>();
        public readonly IGameDataService GameData = Substitute.For<IGameDataService>();
        public readonly IGameDataEditStore Store = Substitute.For<IGameDataEditStore>();
        public readonly NpcData Worm = new() { Id = WormId, Name = "Worm", IsMonster = true, Level = 5, Hp = 500, Money = Gold, ItemGroup = WormDropRow };
        public readonly NpcItemData Drops = new() { Index = WormDropRow, IsMonster = true, Item1 = Ruby, Percent1 = 10_000 };
        public readonly AdminMonsterEditor Editor;
        public readonly UserSession Gm;
        public readonly NpcInstance Living;
        public Dictionary<int, int[]> Groups = new() { [Group] = [Topaz] };

        public Fixture()
        {
            Client.Id.Returns(Guid.NewGuid());
            Client.SendPacket(Arg.Do<Packet>(Sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Gm = Sessions.CreateSession(Client, 1, 1);
            Gm.Name = "Zeus";
            Gm.IsGM = true;

            GameData.MonsterTable.Returns(new Dictionary<int, NpcData> { [WormId] = Worm });
            GameData.NpcTable.Returns(new Dictionary<int, NpcData>());
            GameData.GetNpcItem(WormDropRow, true).Returns(_ => Drops);
            GameData.MakeItemGroupTable.Returns(_ => Groups);
            GameData.GetItem(Ruby).Returns(new ItemData { Num = Ruby, Name = "Ruby" });
            GameData.GetItem(Topaz).Returns(new ItemData { Num = Topaz, Name = "Topaz" });
            GameData.When(data => data.PutItemGroup(Arg.Any<int>(), Arg.Any<int[]>()))
                .Do(call => Groups = new Dictionary<int, int[]>(Groups) { [call.ArgAt<int>(0)] = call.ArgAt<int[]>(1) });
            Store.SaveMonsterAsync(Arg.Any<NpcData>()).Returns(true);

            Editor = new AdminMonsterEditor(Sessions, GameData, Store,
                new TimeWeatherBroadcastService(Sessions, Substitute.For<ILogger<TimeWeatherBroadcastService>>()),
                Substitute.For<ILogger<AdminMonsterEditor>>());
            Living = Sessions.Regions.SpawnNpc(new NpcInstance
            {
                IsMonster = true, NpcId = WormId, Name = "Worm", ZoneId = 21, Hp = 500, MaxHp = 500, GoldDrop = Gold, DropItemGroup = WormDropRow,
            });
        }

        public Task Send(byte sub, Action<Packet> body)
        {
            var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
            body(packet);
            packet.ResetOffset();
            return Editor.HandleAsync(Gm, sub, packet);
        }

        public (bool Ok, string Message) Result()
        {
            var packet = Sent.Last(p => Sub(p) == AckResult);
            packet.ResetOffset();
            packet.ReadByte();
            return (packet.ReadByte() == AdminPanelPacketWriter.Granted, packet.ReadString());
        }
    }

    private static byte Sub(Packet packet)
    {
        packet.ResetOffset();
        return packet.ReadByte();
    }

    private static Action<Packet> SheetEdit(params (byte Field, int Value)[] edits) => packet =>
    {
        packet.WriteByte(AdminPanelPacketWriter.Granted);
        packet.WriteInt(WormId);
        packet.WriteByte((byte)edits.Length);
        foreach (var (field, value) in edits)
        {
            packet.WriteByte(field);
            packet.WriteInt(value);
        }
    };

    [Fact]
    public async Task TheTargetsSheetCarriesItsFieldsAndDropRow()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqMonster, packet =>
        {
            packet.WriteByte(AdminMonsterEditor.ByTarget);
            packet.WriteInt(f.Living.UniqueId);
        });

        var sheet = f.Sent.Last(p => Sub(p) == AdminMonsterEditor.AckMonster);
        sheet.ResetOffset();
        sheet.ReadByte();
        sheet.ReadByte().Should().Be(AdminPanelPacketWriter.Granted);
        sheet.ReadInt().Should().Be(WormId);
        sheet.ReadSByteString().Should().Be("Worm");
        sheet.ReadByte();
        sheet.ReadInt();
        sheet.ReadInt().Should().Be(1, "one worm is alive");
        int fields = sheet.ReadByte();
        fields.Should().Be(MonsterSheet.Fields.Count);
        for (var i = 0; i < fields; i++)
        {
            sheet.ReadByte();
            sheet.ReadSByteString();
            sheet.ReadInt();
            sheet.ReadInt();
            sheet.ReadInt();
        }

        sheet.ReadShort().Should().Be(WormDropRow);
        sheet.ReadByte().Should().Be(AdminPanelPacketWriter.Granted);
        sheet.ReadUShort().Should().Be(1);
        sheet.ReadByte().Should().Be(NpcItemData.DropSlots);
        sheet.ReadInt().Should().Be(Ruby);
        sheet.ReadShort().Should().Be(10_000);
        sheet.ReadSByteString().Should().Be("Ruby");
    }

    [Fact]
    public async Task SetChangesTheTableAndTheLivingMonsterWithoutSaving()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqMonsterSet, SheetEdit((GoldField, 2500), (LevelField, 9)));

        f.Worm.Money.Should().Be(2500);
        f.Worm.Level.Should().Be(9);
        f.Living.GoldDrop.Should().Be(2500);
        f.Living.Level.Should().Be(9);
        f.Result().Ok.Should().BeTrue();
        await f.Store.DidNotReceive().SaveMonsterAsync(Arg.Any<NpcData>());
    }

    [Fact]
    public async Task PersistSavesTheSheetInTheDatabase()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqMonsterPersist, SheetEdit((GoldField, 2500)));

        await f.Store.Received(1).SaveMonsterAsync(f.Worm);
        f.Result().Message.Should().Contain("saved to the database");
    }

    [Fact]
    public async Task AValueOutsideItsFieldIsRefusedAndNothingChanges()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqMonsterSet, SheetEdit((GoldField, 2500), (LevelField, 0)));

        f.Worm.Money.Should().Be(Gold);
        f.Result().Ok.Should().BeFalse();
    }

    [Fact]
    public async Task DropsAreReplacedLiveAndSavedOnPersist()
    {
        var f = new Fixture();
        NpcItemData? put = null;
        f.GameData.When(data => data.PutNpcItem(Arg.Any<NpcItemData>())).Do(call => put = call.Arg<NpcItemData>());

        await f.Send(AdminMonsterEditor.ReqDropsPersist, packet =>
        {
            packet.WriteByte(AdminPanelPacketWriter.Granted);
            packet.WriteInt(WormId);
            packet.WriteShort(WormDropRow);
            packet.WriteInt(Group);
            packet.WriteShort(2500);
            for (var slot = 1; slot < NpcItemData.DropSlots; slot++)
            {
                packet.WriteInt(0);
                packet.WriteShort(0);
            }
        });

        put.Should().NotBeNull();
        put!.Item1.Should().Be(Group);
        put.Percent1.Should().Be(2500);
        await f.Store.Received(1).SaveDropRowAsync(put);
    }

    [Fact]
    public async Task ADropOfAMissingItemIsRefused()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqDropsSet, packet =>
        {
            packet.WriteByte(AdminPanelPacketWriter.Granted);
            packet.WriteInt(WormId);
            packet.WriteShort(WormDropRow);
            for (var slot = 0; slot < NpcItemData.DropSlots; slot++)
            {
                packet.WriteInt(slot == 2 ? 399999999 : 0);
                packet.WriteShort(0);
            }
        });

        f.Result().Should().Be((false, "Slot 3: no item 399999999."));
        f.GameData.DidNotReceive().PutNpcItem(Arg.Any<NpcItemData>());
    }

    [Fact]
    public async Task AnItemGroupTakesOnlyKnownItems()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqItemGroupSet, packet =>
        {
            packet.WriteInt(Group);
            packet.WriteUShort(2);
            packet.WriteInt(Ruby);
            packet.WriteInt(Topaz);
        });
        f.Groups[Group].Should().Equal(Ruby, Topaz);

        await f.Send(AdminMonsterEditor.ReqItemGroupSet, packet =>
        {
            packet.WriteInt(Group);
            packet.WriteUShort(1);
            packet.WriteInt(123);
        });
        f.Result().Ok.Should().BeFalse();
        f.Groups[Group].Should().Equal(Ruby, Topaz);
    }

    [Fact]
    public async Task TheGroupListFindsGroupsByNumberOrByAnItemInside()
    {
        var f = new Fixture();
        f.Groups = new Dictionary<int, int[]> { [Group] = [Topaz], [9100] = [Ruby, Topaz], [120] = [Ruby] };

        async Task<List<(int Group, int Count, List<int> Samples)>> List(string query)
        {
            f.Sent.Clear();
            await f.Send(AdminMonsterEditor.ReqItemGroups, packet => packet.WriteUtf8String(query));
            var reply = f.Sent.Single(p => Sub(p) == AdminMonsterEditor.AckItemGroups);
            reply.ResetOffset();
            reply.ReadByte();
            reply.ReadUShort();
            int rows = reply.ReadByte();
            var found = new List<(int, int, List<int>)>();
            for (var i = 0; i < rows; i++)
            {
                var group = reply.ReadInt();
                int count = reply.ReadUShort();
                int samples = reply.ReadByte();
                var items = new List<int>();
                for (var j = 0; j < samples; j++) items.Add(reply.ReadInt());
                found.Add((group, count, items));
            }
            return found;
        }

        (await List("9")).Select(row => row.Group).Should().Equal(Group, 9100);
        (await List("ruby")).Select(row => row.Group).Should().Equal(120, 9100);
        (await List("")).Should().HaveCount(3);
        (await List("9100")).Single().Should().Match<(int Group, int Count, List<int> Samples)>(row => row.Count == 2 && row.Samples.SequenceEqual(new[] { Ruby, Topaz }));
    }

    [Fact]
    public void ASureDropLandsOnEveryKillAndCoinsStayInTheirRange()
    {
        var row = new NpcItemData { Item1 = Ruby, Percent1 = 10_000, Item2 = Group, Percent2 = 0 };

        var tally = DropRoll.Tally(Gold, row, new Dictionary<int, int[]>(), 500, 0, 0, 0, _ => true, new Random(7));

        tally.Items[Ruby].Should().Be(500);
        tally.ItemlessKills.Should().Be(0);
        tally.CoinMin.Should().BeGreaterThanOrEqualTo(Gold * DropRoll.CoinRollMinPercent / 100);
        tally.CoinMax.Should().BeLessThanOrEqualTo(Gold);
    }

    [Theory]
    [InlineData(1000, 0, 0, 1000)]
    [InlineData(1000, 50, 0, 1500)]
    [InlineData(1000, 0, 100, 2000)]
    [InlineData(1000, 50, 100, 3000)]
    public void PremiumThenTheDropEventRaiseTheChance(int percent, int premium, int dropEvent, int expected) =>
        DropRoll.Chance(percent, premium, dropEvent).Should().Be(expected);

    [Fact]
    public async Task TheSimulatorRollsTheMonsterWhereTheTableSays()
    {
        var f = new Fixture();

        await f.Send(AdminMonsterEditor.ReqDropSimulate, packet =>
        {
            packet.WriteByte(AdminPanelPacketWriter.Granted);
            packet.WriteInt(WormId);
            packet.WriteInt(200);
            packet.WriteByte(0);
            packet.WriteByte(0);
            packet.WriteByte(0);
        });

        var result = f.Sent.Last(p => Sub(p) == AdminMonsterEditor.AckDropSimulation);
        result.ResetOffset();
        result.ReadByte();
        result.ReadByte();
        result.ReadInt().Should().Be(WormId);
        result.ReadInt().Should().Be(200);
        result.ReadLong().Should().BeGreaterThan(0);
        result.ReadInt();
        result.ReadInt();
        result.ReadInt().Should().Be(0);
        result.ReadUShort().Should().Be(1);
        result.ReadInt().Should().Be(Ruby);
        result.ReadInt().Should().Be(200);
    }
}
