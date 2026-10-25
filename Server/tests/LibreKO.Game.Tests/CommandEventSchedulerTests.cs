using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class CommandEventSchedulerTests
{
    private static readonly DateTime Saturday2100 = new(2026, 10, 24, 21, 0, 0);

    private sealed record Rule(ScheduleRepeat Repeat, DateOnly? Date, DayOfWeek? Day, byte DayOfMonth, byte Hour, byte Minute) : IScheduleRule;

    private sealed class Fixture
    {
        public readonly SessionManager Sessions = new();
        public readonly IEventSystemsPacketCoordinator EventSystems = Substitute.For<IEventSystemsPacketCoordinator>();
        public readonly IVanguardService Vanguard = Substitute.For<IVanguardService>();
        public readonly IEventStartRules Rules = Substitute.For<IEventStartRules>();
        public readonly CommandEventScheduler Scheduler;
        public bool WantedOn;

        public Fixture()
        {
            Rules.For(Arg.Any<EventScheduleKind>(), Arg.Any<IEnumerable<int>>()).Returns(Array.Empty<IScheduleRule>());
            Vanguard.Enabled.Returns(_ => WantedOn);
            Vanguard.When(v => v.StartAsync()).Do(_ => WantedOn = true);
            Vanguard.When(v => v.StopAsync()).Do(_ => WantedOn = false);
            Scheduler = new CommandEventScheduler(Sessions, EventSystems, Vanguard, Rules, new ScheduleClock(),
                Options.Create(new GameServerSettings()), Substitute.For<ILogger<CommandEventScheduler>>());
        }

        public void Starts(EventScheduleKind kind, params IScheduleRule[] rules) =>
            Rules.For(kind, Arg.Any<IEnumerable<int>>()).Returns(rules);
    }

    [Fact]
    public async Task AStoredWarStartOpensABattleZoneOnceInThatMinute()
    {
        var f = new Fixture();
        f.Starts(EventScheduleKind.NationWar, new Rule(ScheduleRepeat.Weekly, null, DayOfWeek.Saturday, 0, 21, 0));

        await f.Scheduler.TickAsync(DateTime.UtcNow, Saturday2100);
        await f.Scheduler.TickAsync(DateTime.UtcNow, Saturday2100.AddSeconds(30));

        await f.EventSystems.Received(1).OpenBattleZoneAsync(Arg.Is(BattleZoneManager.BATTLEZONE_OPEN), Arg.Any<byte>());
    }

    [Fact]
    public async Task NothingStartsWithoutStoredStarts()
    {
        var f = new Fixture();

        await f.Scheduler.TickAsync(DateTime.UtcNow, Saturday2100);

        await f.EventSystems.DidNotReceiveWithAnyArgs().OpenBattleZoneAsync(default, default);
        await f.Vanguard.DidNotReceive().StartAsync();
    }

    [Fact]
    public async Task ScheduledWantedStopsAfterItsMinutes()
    {
        var f = new Fixture();
        f.Starts(EventScheduleKind.Wanted, new Rule(ScheduleRepeat.Daily, null, null, 0, 21, 0));
        var start = new DateTime(2026, 10, 24, 19, 0, 0, DateTimeKind.Utc);

        await f.Scheduler.TickAsync(start, Saturday2100);
        f.WantedOn.Should().BeTrue();

        await f.Scheduler.TickAsync(start.AddMinutes(new EventSettings().WantedMinutes - 1), Saturday2100.AddMinutes(59));
        f.WantedOn.Should().BeTrue();

        await f.Scheduler.TickAsync(start.AddMinutes(new EventSettings().WantedMinutes), Saturday2100.AddMinutes(60));
        f.WantedOn.Should().BeFalse();
    }
}
