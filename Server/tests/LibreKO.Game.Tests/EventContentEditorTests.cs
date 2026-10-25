using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
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

public class EventContentEditorTests
{
    private const byte AckResult = 0x11;
    private const int Ruby = 389010000;
    private const int Worm = 750;
    private const byte Moradon = 21;

    private static ServiceProvider Database()
    {
        var services = new ServiceCollection();
        var name = $"EventContent_{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(name)
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SavingALotteryReplacesItsRewardsAndDeletingTakesItsStartsAlong()
    {
        using var provider = Database();
        var store = new EventContentStore(provider.GetRequiredService<IServiceScopeFactory>());

        var id = await store.SaveLotteryAsync(new LotteryEventData { Name = "Moradon Draw", DurationMinutes = 15, UserLimit = 100, ReqItemId = 900000000, ReqItemCount = 1000 },
            [new LotteryRewardData { Place = 1, ItemId = Ruby, Count = 2 }, new LotteryRewardData { Place = 2, ItemId = Ruby, Count = 1 }]);
        await store.SaveLotteryAsync(new LotteryEventData { Id = id, Name = "Moradon Draw", DurationMinutes = 20, UserLimit = 50, ReqItemId = 900000000, ReqItemCount = 1000 },
            [new LotteryRewardData { Place = 1, ItemId = Ruby, Count = 5 }]);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.LotteryEvents.Single().DurationMinutes.Should().Be(20);
            db.LotteryRewards.Should().ContainSingle().Which.Count.Should().Be(5);
            db.LotterySchedules.Add(new LotteryScheduleData { Id = 1, LotteryId = id, Hour = 12 });
            await db.SaveChangesAsync();
        }

        (await store.DeleteLotteryAsync(id)).Should().BeTrue();
        using var after = provider.CreateScope();
        var cleared = after.ServiceProvider.GetRequiredService<AppDbContext>();
        cleared.LotteryEvents.Should().BeEmpty();
        cleared.LotteryRewards.Should().BeEmpty();
        cleared.LotterySchedules.Should().BeEmpty();
    }

    [Fact]
    public async Task ARaceKeepsItsTargetsInOrder()
    {
        using var provider = Database();
        var store = new EventContentStore(provider.GetRequiredService<IServiceScopeFactory>());

        var id = await store.SaveRaceAsync(new CollectionRaceData { Name = "Worm Hunt", ZoneId = Moradon, MinLevel = 1, MaxLevel = 30, DurationMinutes = 60, MaxWinners = 10 },
            [
                new CollectionRaceObjectiveData { Kind = CollectionRaceObjectiveKind.Monster, TargetId = Worm, Count = 20 },
                new CollectionRaceObjectiveData { Kind = CollectionRaceObjectiveKind.Item, TargetId = Ruby, Count = 3 },
            ],
            [new CollectionRaceRewardData { ItemId = Ruby, ItemCount = 1, Rate = 50 }]);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CollectionRaceObjectives.Where(o => o.RaceId == id).OrderBy(o => o.Ordinal).Select(o => o.Kind)
            .Should().Equal(CollectionRaceObjectiveKind.Monster, CollectionRaceObjectiveKind.Item);
        db.CollectionRaceRewards.Should().ContainSingle().Which.Rate.Should().Be(50);
    }

    private sealed class Fixture
    {
        public readonly SessionManager Sessions = new();
        public readonly List<Packet> Sent = [];
        public readonly IGameDataService GameData = Substitute.For<IGameDataService>();
        public readonly IEventContentStore Store = Substitute.For<IEventContentStore>();
        public readonly IAdminEventScheduleEditor Schedules = Substitute.For<IAdminEventScheduleEditor>();
        public readonly ILotteryService Lottery = Substitute.For<ILotteryService>();
        public readonly AdminEventContentEditor Editor;
        public readonly UserSession Gm;

        public Fixture()
        {
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            client.SendPacket(Arg.Do<Packet>(Sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Gm = Sessions.CreateSession(client, 1, 1);
            Gm.IsGM = true;
            var draw = new LotteryEventData { Id = 1, Name = "Moradon Draw", DurationMinutes = 15, UserLimit = 100, ReqItemId = 900000000, ReqItemCount = 1000 };
            GameData.LotteryEventTable.Returns(new Dictionary<int, LotteryEventData> { [1] = draw });
            GameData.LotteryRewardsByEvent.Returns(Array.Empty<LotteryRewardData>().ToLookup(r => r.LotteryId));
            GameData.CollectionRaceTable.Returns(new Dictionary<int, CollectionRaceData>());
            GameData.ZoneInfoTable.Returns(new Dictionary<short, ZoneInfoData> { [Moradon] = new() });
            GameData.MonsterTable.Returns(new Dictionary<int, NpcData> { [Worm] = new() { Id = Worm, Name = "Worm", IsMonster = true } });
            GameData.GetItem(Ruby).Returns(new ItemData { Num = Ruby, Name = "Ruby" });
            Store.SaveLotteryAsync(default!, default!).ReturnsForAnyArgs(1);
            var races = Substitute.For<ICollectionRaceService>();
            races.ActiveRaces.Returns(Array.Empty<ActiveCollectionRace>());
            Editor = new AdminEventContentEditor(GameData, Store, Schedules, Lottery, races, Substitute.For<ILogger<AdminEventContentEditor>>());
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

    private static Action<Packet> Lottery(int rewardItem) => packet =>
    {
        packet.WriteInt(1);
        packet.WriteSByteString("Moradon Draw");
        packet.WriteInt(15);
        packet.WriteInt(100);
        packet.WriteInt(900000000);
        packet.WriteInt(1000);
        packet.WriteByte(AdminPanelPacketWriter.Granted);
        packet.WriteByte(1);
        packet.WriteInt(1);
        packet.WriteInt(rewardItem);
        packet.WriteInt(2);
    };

    [Fact]
    public async Task ALotteryIsSavedReloadedAndSentBack()
    {
        var f = new Fixture();

        await f.Send(AdminEventContentEditor.ReqLotterySave, Lottery(Ruby));

        await f.Store.Received(1).SaveLotteryAsync(Arg.Is<LotteryEventData>(d => d.Id == 1 && d.AutoStart),
            Arg.Is<IReadOnlyList<LotteryRewardData>>(r => r.Single().ItemId == Ruby && r.Single().Count == 2));
        await f.GameData.Received(1).ReloadEventsAsync(Arg.Any<CancellationToken>());
        await f.Schedules.Received(1).SendSchedulesAsync(f.Gm);
        f.Result().Ok.Should().BeTrue();
        f.Sent.Should().Contain(p => IsSub(p, AdminEventContentEditor.AckLottery));
    }

    [Fact]
    public async Task ALotteryRewardOfAMissingItemIsRefused()
    {
        var f = new Fixture();

        await f.Send(AdminEventContentEditor.ReqLotterySave, Lottery(123));

        f.Result().Should().Be((false, "No item 123."));
        await f.Store.DidNotReceiveWithAnyArgs().SaveLotteryAsync(default!, default!);
    }

    [Fact]
    public async Task ARaceWithoutTargetsIsRefused()
    {
        var f = new Fixture();

        await f.Send(AdminEventContentEditor.ReqRaceSave, packet =>
        {
            packet.WriteInt(0);
            packet.WriteSByteString("Worm Hunt");
            packet.WriteByte(Moradon);
            packet.WriteByte(1);
            packet.WriteByte(30);
            packet.WriteInt(60);
            packet.WriteInt(10);
            packet.WriteByte(0);
            packet.WriteByte(0);
            packet.WriteByte(0);
        });

        f.Result().Should().Be((false, $"A race has 1 to {AdminEventContentEditor.MaxRows} targets."));
    }

    [Fact]
    public async Task TheRunningDrawCannotBeDeleted()
    {
        var f = new Fixture();
        var running = new ActiveLottery(f.GameData.LotteryEventTable[1], DateTime.UtcNow.AddMinutes(5));
        f.Lottery.ActiveEvent.Returns(running);

        await f.Send(AdminEventContentEditor.ReqLotteryDelete, packet => packet.WriteInt(1));

        f.Result().Ok.Should().BeFalse();
        await f.Store.DidNotReceiveWithAnyArgs().DeleteLotteryAsync(default);
    }

    private static bool IsSub(Packet packet, byte sub)
    {
        packet.ResetOffset();
        return packet.ReadByte() == sub;
    }
}
