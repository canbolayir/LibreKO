using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class GameplayTipTests
{
    private const byte AckResult = 0x11;
    private static readonly DateTime Login = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static List<GameplayTipData> Tips(params (int Id, GameLanguage Language, bool Enabled)[] rows) =>
        rows.Select(row => new GameplayTipData { Id = row.Id, Language = row.Language, Enabled = row.Enabled, Text = $"tip {row.Id}" }).ToList();

    [Fact]
    public void EachTipComesOnceAnIntervalApartAfterTheFirstWait()
    {
        var rotation = new GameplayTipRotation();
        var tips = Tips((1, GameLanguage.English, true), (2, GameLanguage.English, true));

        rotation.Next(Login, Interval, tips, new Random(1)).Should().BeNull();
        rotation.Next(Login + TimeSpan.FromMinutes(4), Interval, tips, new Random(1)).Should().BeNull();
        var first = rotation.Next(Login + Interval, Interval, tips, new Random(1));
        rotation.Next(Login + Interval + TimeSpan.FromMinutes(1), Interval, tips, new Random(1)).Should().BeNull();
        var second = rotation.Next(Login + Interval * 2, Interval, tips, new Random(1));
        rotation.Next(Login + Interval * 3, Interval, tips, new Random(1)).Should().BeNull();

        new[] { first!.Id, second!.Id }.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void APlayerGetsTipsInTheirLanguageOrElseEnglish()
    {
        var tips = Tips((1, GameLanguage.English, true), (2, GameLanguage.Spanish, true), (3, GameLanguage.Spanish, false));

        GameplayTipRotation.InLanguage(tips, GameLanguage.Spanish).Select(tip => tip.Id).Should().Equal(2);
        GameplayTipRotation.InLanguage(tips, GameLanguage.Chinese).Select(tip => tip.Id).Should().Equal(1);
        GameplayTipRotation.InLanguage(tips, GameLanguage.English).Select(tip => tip.Id).Should().Equal(1);
    }

    [Fact]
    public async Task TheStoreSavesTipsAndTheSettingsRow()
    {
        var services = new ServiceCollection();
        var name = $"Tips_{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(name)
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        using var provider = services.BuildServiceProvider();
        var store = new GameplayTipStore(provider.GetRequiredService<IServiceScopeFactory>());

        int first = await store.SaveTipAsync(0, GameLanguage.English, "Press K for skills.", true);
        int second = await store.SaveTipAsync(0, GameLanguage.Spanish, "Pulsa K.", true);
        await store.SaveTipAsync(first, GameLanguage.English, "Press K to open your skills.", false);
        await store.SaveSettingsAsync(false, 10);
        (await store.DeleteTipAsync(second)).Should().BeTrue();

        var (tips, settings) = await store.LoadAsync();
        tips.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = first, Text = "Press K to open your skills.", Enabled = false });
        settings.Should().BeEquivalentTo(new { Enabled = false, IntervalMinutes = 10 });
    }

    private sealed class Fixture
    {
        public readonly List<Packet> Sent = [];
        public readonly IGameplayTipStore Store = Substitute.For<IGameplayTipStore>();
        public readonly IGameplayTipService Service = Substitute.For<IGameplayTipService>();
        public readonly AdminTipEditor Editor;
        public readonly UserSession Gm;

        public Fixture()
        {
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            client.SendPacket(Arg.Do<Packet>(Sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Gm = new SessionManager().CreateSession(client, 1, 1);
            Gm.IsGM = true;
            Service.Tips.Returns(Tips((4, GameLanguage.English, true)));
            Service.Settings.Returns(new GameplayTipSettingsData { Enabled = true, IntervalMinutes = 5 });
            Store.SaveTipAsync(default, default, default!, default).ReturnsForAnyArgs(5);
            Editor = new AdminTipEditor(Store, Service, Substitute.For<ILogger<AdminTipEditor>>());
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
            var packet = Sent.Last(p => { p.ResetOffset(); return p.ReadByte() == AckResult; });
            packet.ResetOffset();
            packet.ReadByte();
            return (packet.ReadByte() == AdminPanelPacketWriter.Granted, packet.ReadString());
        }
    }

    private static Action<Packet> Save(int id, string text) => packet =>
    {
        packet.WriteInt(id);
        packet.WriteByte((byte)GameLanguage.English);
        packet.WriteByte(AdminPanelPacketWriter.Granted);
        packet.WriteString(text);
    };

    [Fact]
    public async Task ATipIsSavedReloadedAndTheListSentBack()
    {
        var f = new Fixture();

        await f.Send(AdminTipEditor.ReqTipSave, Save(0, "  Press K to open your skills.  "));

        await f.Store.Received(1).SaveTipAsync(0, GameLanguage.English, "Press K to open your skills.", true);
        await f.Service.Received(1).ReloadAsync();
        f.Result().Should().Be((true, "Tip 5 saved."));
        var list = f.Sent.Last();
        list.ResetOffset();
        list.ReadByte().Should().Be(AdminTipEditor.AckTips);
        list.ReadByte().Should().Be(AdminPanelPacketWriter.Granted);
        list.ReadShort().Should().Be(5);
        list.ReadShort().Should().Be(1);
        list.ReadInt().Should().Be(4);
        list.ReadByte().Should().Be((byte)GameLanguage.English);
        list.ReadByte().Should().Be(AdminPanelPacketWriter.Granted);
        list.ReadString().Should().Be("tip 4");
    }

    [Fact]
    public async Task AnEmptyOrTooLongTipIsRefused()
    {
        var f = new Fixture();

        await f.Send(AdminTipEditor.ReqTipSave, Save(0, "   "));
        f.Result().Should().Be((false, "Write the tip first."));
        await f.Send(AdminTipEditor.ReqTipSave, Save(0, new string('a', GameplayTipData.MaxTextLength + 1)));
        f.Result().Ok.Should().BeFalse();
        await f.Send(AdminTipEditor.ReqTipSave, Save(9, "Missing"));
        f.Result().Should().Be((false, "No tip 9."));
        await f.Store.DidNotReceiveWithAnyArgs().SaveTipAsync(default, default, default!, default);
    }

    [Fact]
    public async Task TheIntervalStaysWithinItsBounds()
    {
        var f = new Fixture();

        await f.Send(AdminTipEditor.ReqTipSettings, packet => { packet.WriteByte(AdminPanelPacketWriter.Granted); packet.WriteShort(0); });
        f.Result().Ok.Should().BeFalse();
        await f.Send(AdminTipEditor.ReqTipSettings, packet => { packet.WriteByte(AdminPanelPacketWriter.Denied); packet.WriteShort(15); });

        await f.Store.Received(1).SaveSettingsAsync(false, 15);
        f.Result().Should().Be((true, "Tips are off."));
    }

    [Fact]
    public void ATipIsASenderlessChatLine()
    {
        var packet = ChatPacketWriter.Tip("Press K to open your skills.");

        packet.ResetOffset();
        packet.ReadByte().Should().Be((byte)ChatType.GameplayTip);
        packet.ReadByte();
        packet.ReadInt().Should().Be(ChatPacketWriter.NoSender);
        packet.ReadSByteString().Should().BeEmpty();
        packet.ReadString().Should().Be("Press K to open your skills.");
    }
}
