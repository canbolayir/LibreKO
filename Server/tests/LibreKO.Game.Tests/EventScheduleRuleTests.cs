using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class EventScheduleRuleTests
{
    private const byte AckResult = 0x11;

    private sealed record Rule(ScheduleRepeat Repeat, DateOnly? Date, DayOfWeek? Day, byte DayOfMonth, byte Hour, byte Minute) : IScheduleRule;

    [Fact]
    public void EachRepeatMatchesItsOwnDays()
    {
        var friday = new DateTime(2026, 10, 23, 20, 30, 0);

        new Rule(ScheduleRepeat.Daily, null, null, 0, 20, 30).Matches(friday).Should().BeTrue();
        new Rule(ScheduleRepeat.Weekly, null, DayOfWeek.Friday, 0, 20, 30).Matches(friday).Should().BeTrue();
        new Rule(ScheduleRepeat.Weekly, null, DayOfWeek.Monday, 0, 20, 30).Matches(friday).Should().BeFalse();
        new Rule(ScheduleRepeat.Once, new DateOnly(2026, 10, 23), null, 0, 20, 30).Matches(friday).Should().BeTrue();
        new Rule(ScheduleRepeat.Once, new DateOnly(2026, 10, 24), null, 0, 20, 30).Matches(friday).Should().BeFalse();
        new Rule(ScheduleRepeat.Monthly, null, null, 23, 20, 30).Matches(friday).Should().BeTrue();
        new Rule(ScheduleRepeat.Daily, null, null, 0, 20, 31).Matches(friday).Should().BeFalse();
    }

    [Fact]
    public void AMonthlyStartPastTheMonthsEndFallsOnItsLastDay()
    {
        var rule = new Rule(ScheduleRepeat.Monthly, null, null, 31, 12, 0);

        rule.Occurrences(new DateOnly(2027, 2, 1), new DateOnly(2027, 3, 31))
            .Should().Equal(new DateTime(2027, 2, 28, 12, 0, 0), new DateTime(2027, 3, 31, 12, 0, 0));
    }

    [Theory]
    [InlineData(ScheduleRepeat.Weekly, 5, 0)]
    [InlineData(ScheduleRepeat.Once, 0, 0)]
    [InlineData(ScheduleRepeat.Monthly, 0, 0)]
    [InlineData(ScheduleRepeat.Monthly, 0, 32)]
    [InlineData(ScheduleRepeat.Daily, 24, 0)]
    public void AnIncompleteRuleIsRefused(ScheduleRepeat repeat, byte hour, byte dayOfMonth) =>
        ScheduleRule.Problem(repeat, null, null, dayOfMonth, hour, 0).Should().NotBeNull();

    [Fact]
    public void TheEventsWindowShowsAOneTimeStartOnlyInTheComingWeek()
    {
        var today = new DateOnly(2026, 10, 23);

        EventSchedulePacketCoordinator.Slots([new Rule(ScheduleRepeat.Once, new DateOnly(2026, 10, 25), null, 0, 18, 0)], 0, today)
            .Should().ContainSingle().Which.Should().Be(EventSchedulePacketCoordinator.Slot(DayOfWeek.Sunday, 18, 0, 0));
        EventSchedulePacketCoordinator.Slots([new Rule(ScheduleRepeat.Once, new DateOnly(2026, 11, 25), null, 0, 18, 0)], 0, today)
            .Should().BeEmpty();
    }

    [Fact]
    public void StoredStartsReplaceTheSettingsHours()
    {
        var gameData = Substitute.For<IGameDataService>();
        gameData.EventStarts.Returns(new List<EventStartData>());
        var rules = new EventStartRules(gameData);

        rules.For(EventScheduleKind.Krowaz, [9, 22]).Select(r => r.Hour).Should().Equal(9, 22);

        gameData.EventStarts.Returns(new List<EventStartData>
        {
            new() { Id = 1, Kind = (byte)EventScheduleKind.Krowaz, Repeat = ScheduleRepeat.Weekly, Day = DayOfWeek.Saturday, Hour = 21, Minute = 30 },
            new() { Id = 2, Kind = (byte)EventScheduleKind.Bowl, Hour = 14 },
        });
        var krowaz = rules.For(EventScheduleKind.Krowaz, [9, 22]);
        krowaz.Should().ContainSingle().Which.Minute.Should().Be(30);
        EventStartRules.StartsAt(rules, EventScheduleKind.Krowaz, [9, 22], new DateTime(2026, 10, 24, 21, 30, 0)).Should().BeTrue();
        EventStartRules.StartsAt(rules, EventScheduleKind.Krowaz, [9, 22], new DateTime(2026, 10, 24, 9, 0, 0)).Should().BeFalse();
    }

    private static ServiceProvider Database()
    {
        var services = new ServiceCollection();
        var name = $"EventSchedules_{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(name)
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheFirstStoredStartKeepsTheSettingsHoursBesideIt()
    {
        using var provider = Database();
        var store = new EventScheduleStore(provider.GetRequiredService<IServiceScopeFactory>());

        await store.CopyHoursAsync(EventScheduleKind.Bifrost, [5, 1]);
        await store.CopyHoursAsync(EventScheduleKind.Bifrost, [7]);
        (await store.SaveAsync(EventScheduleKind.Bifrost, 0, 0,
            new RuleFields(ScheduleRepeat.Monthly, null, DayOfWeek.Monday, 15, 20, 45), new TempleRuleFields(0, 0, 0))).Should().BeTrue();

        using var scope = provider.CreateScope();
        var starts = scope.ServiceProvider.GetRequiredService<AppDbContext>().EventStarts.OrderBy(s => s.Id).ToList();
        starts.Select(s => (s.Repeat, s.Hour, s.Minute)).Should().Equal(
            (ScheduleRepeat.Daily, (byte)1, (byte)0), (ScheduleRepeat.Daily, (byte)5, (byte)0), (ScheduleRepeat.Monthly, (byte)20, (byte)45));
        starts[2].Day.Should().BeNull("a monthly start keeps no weekday");
        starts[2].DayOfMonth.Should().Be(15);
    }

    [Fact]
    public async Task ATempleStartIsSavedWithItsLevelsAndRemoved()
    {
        using var provider = Database();
        var store = new EventScheduleStore(provider.GetRequiredService<IServiceScopeFactory>());

        await store.SaveAsync(EventScheduleKind.Temple, (int)TempleEvent.Chaos, 0,
            new RuleFields(ScheduleRepeat.Once, new DateOnly(2026, 12, 24), null, 0, 19, 0), new TempleRuleFields(30, 70, 5));

        using (var scope = provider.CreateScope())
        {
            var row = scope.ServiceProvider.GetRequiredService<AppDbContext>().TempleEventSchedules.Single();
            (row.Event, row.Repeat, row.Date, row.MinLevel, row.MaxLevel, row.CountdownMinutes)
                .Should().Be((TempleEvent.Chaos, ScheduleRepeat.Once, new DateOnly(2026, 12, 24), (byte)30, (byte)70, (byte)5));
        }

        (await store.DeleteAsync(EventScheduleKind.Temple, (int)TempleEvent.JuraidMountain, 1)).Should().BeFalse("the start belongs to Chaos");
        (await store.DeleteAsync(EventScheduleKind.Temple, (int)TempleEvent.Chaos, 1)).Should().BeTrue();
    }

    private sealed class EditorFixture
    {
        public readonly SessionManager Sessions = new();
        public readonly List<Packet> Sent = [];
        public readonly IGameDataService GameData = Substitute.For<IGameDataService>();
        public readonly IEventScheduleStore Store = Substitute.For<IEventScheduleStore>();
        public readonly AdminEventScheduleEditor Editor;
        public readonly UserSession Gm;

        public EditorFixture()
        {
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            client.SendPacket(Arg.Do<Packet>(Sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Gm = Sessions.CreateSession(client, 1, 1);
            Gm.IsGM = true;
            GameData.EventStarts.Returns(new List<EventStartData>());
            GameData.EventDurations.Returns(new List<EventDurationData>());
            GameData.TempleEventSchedules.Returns(new List<TempleEventScheduleData>());
            GameData.CollectionRaceTable.Returns(new Dictionary<int, CollectionRaceData>());
            GameData.LotteryEventTable.Returns(new Dictionary<int, LotteryEventData>());
            Store.SaveAsync(default, default, default, default!, default!).ReturnsForAnyArgs(true);
            Editor = new AdminEventScheduleEditor(GameData, Store, new EventStartRules(GameData),
                Options.Create(new GameServerSettings()), Substitute.For<ILogger<AdminEventScheduleEditor>>());
        }

        public Task Save(EventScheduleKind kind, int owner, ScheduleRepeat repeat, byte weekday, byte hour)
        {
            var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
            packet.WriteByte((byte)kind);
            packet.WriteInt(owner);
            packet.WriteInt(0);
            packet.WriteByte((byte)repeat);
            packet.WriteInt(0);
            packet.WriteByte(weekday);
            packet.WriteByte(0);
            packet.WriteByte(hour);
            packet.WriteByte(0);
            packet.WriteByte(20);
            packet.WriteByte(83);
            packet.WriteByte(10);
            packet.ResetOffset();
            return Editor.HandleAsync(Gm, AdminEventScheduleEditor.ReqRuleSave, packet);
        }

        public Task SaveDuration(EventScheduleKind kind, ushort minutes)
        {
            var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
            packet.WriteByte((byte)kind);
            packet.WriteInt(0);
            packet.WriteUShort(minutes);
            packet.ResetOffset();
            return Editor.HandleAsync(Gm, AdminEventScheduleEditor.ReqDurationSave, packet);
        }

        public (bool Ok, string Message) Result()
        {
            var packet = Sent.Last(p => { p.ResetOffset(); return p.ReadByte() == AckResult; });
            packet.ResetOffset();
            packet.ReadByte();
            return (packet.ReadByte() == AdminPanelPacketWriter.Granted, packet.ReadString());
        }
    }

    [Fact]
    public async Task AWeeklyStartWithNoWeekdayIsRefused()
    {
        var f = new EditorFixture();

        await f.Save(EventScheduleKind.Krowaz, 0, ScheduleRepeat.Weekly, AdminEventPacketWriter.NoWeekday, 21);

        f.Result().Should().Be((false, "A weekly start needs a weekday."));
        await f.Store.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default, default!, default!);
    }

    [Fact]
    public async Task ANewStartForAnHourEventFirstKeepsItsSettingsHours()
    {
        var f = new EditorFixture();

        await f.Save(EventScheduleKind.Krowaz, 0, ScheduleRepeat.Weekly, (byte)DayOfWeek.Saturday, 21);

        Received.InOrder(() =>
        {
            f.Store.CopyHoursAsync(EventScheduleKind.Krowaz, Arg.Is<IEnumerable<int>>(hours => hours.SequenceEqual(new[] { 9, 22 })));
            f.Store.SaveAsync(EventScheduleKind.Krowaz, 0, 0,
                new RuleFields(ScheduleRepeat.Weekly, null, DayOfWeek.Saturday, 0, 21, 0), Arg.Any<TempleRuleFields>());
            f.GameData.ReloadEventsAsync(Arg.Any<CancellationToken>());
        });
        f.Result().Ok.Should().BeTrue();
        f.Sent.Should().Contain(p => IsSchedules(p));
    }

    [Fact]
    public async Task TheSiegeDayIsNotEditedHere()
    {
        var f = new EditorFixture();

        await f.Save(EventScheduleKind.CastleSiege, 0, ScheduleRepeat.Daily, AdminEventPacketWriter.NoWeekday, 19);

        f.Result().Ok.Should().BeFalse();
    }

    [Fact]
    public void TimedEventsShowTheirLengthAndTempleEventsNone()
    {
        var f = new EditorFixture();
        var settings = new GameServerSettings();

        var owners = f.Editor.Owners();

        owners.Single(o => o.Kind == EventScheduleKind.Wanted).DurationMinutes.Should().Be((ushort)settings.Events.WantedMinutes);
        owners.Single(o => o.Kind == EventScheduleKind.NationWar).DurationMinutes.Should().Be((ushort)settings.Events.BattleDurationMinutes);
        owners.Single(o => o.Kind == EventScheduleKind.Krowaz).DurationMinutes.Should().Be((ushort)KrowazService.OpenMinutes);
        owners.Where(o => o.Kind == EventScheduleKind.Temple).Should().OnlyContain(o => o.DurationMinutes == 0);
        owners.Single(o => o.Kind == EventScheduleKind.Bifrost).DurationMinutes.Should().Be(0);
    }

    [Fact]
    public async Task AGmSetsAnEventsLengthAndASavedLengthWinsOverTheSettings()
    {
        var f = new EditorFixture();

        await f.SaveDuration(EventScheduleKind.Wanted, 90);
        f.Result().Ok.Should().BeTrue();
        await f.Store.Received(1).SaveDurationAsync(EventScheduleKind.Wanted, 90);

        await f.SaveDuration(EventScheduleKind.Temple, 30);
        f.Result().Ok.Should().BeFalse();
        await f.SaveDuration(EventScheduleKind.SnowWar, 0);
        f.Result().Ok.Should().BeFalse();

        f.GameData.EventDurations.Returns(new List<EventDurationData> { new() { Kind = (byte)EventScheduleKind.Wanted, Minutes = 90 } });
        EventStartRules.MinutesIn(f.GameData, EventScheduleKind.Wanted, 60).Should().Be(90);
        EventStartRules.MinutesIn(f.GameData, EventScheduleKind.NationWar, 20).Should().Be(20);
        EventStartRules.MinutesOf(Substitute.For<IEventStartRules>(), EventScheduleKind.Bowl, 60).Should().Be(60, "a rules source that answers 0 keeps the settings length");
        f.Editor.Handles(AdminEventScheduleEditor.ReqDurationSave).Should().BeTrue();
        f.Editor.Handles(34).Should().BeFalse("34 is the lottery editor's");
    }

    [Fact]
    public void AnHourEventWithoutStoredStartsListsItsSettingsHours()
    {
        var f = new EditorFixture();

        var bowl = f.Editor.Owners().Single(owner => owner.Kind == EventScheduleKind.Bowl);

        (bowl.Flags & AdminEventPacketWriter.FromSettings).Should().NotBe(0);
        bowl.Rules.Select(rule => rule.Schedule.Hour).Should().Equal(14, 19);
        f.Editor.Owners().Where(owner => owner.Kind == EventScheduleKind.Temple).Should().HaveCount(6);
    }

    private static bool IsSchedules(Packet packet)
    {
        packet.ResetOffset();
        return packet.ReadByte() == AdminEventScheduleEditor.AckSchedules;
    }
}
