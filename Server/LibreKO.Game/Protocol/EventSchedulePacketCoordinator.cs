using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.Protocol;

public interface IEventSchedulePacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
    Packet Current();
}

public sealed class EventSchedulePacketCoordinator(
    SessionManager sessionManager,
    IGameDataService gameDataService,
    EventSchedulerService scheduler,
    ICollectionRaceService collectionRaces,
    ILotteryService lottery,
    IKrowazService krowaz,
    IBifrostEventService bifrost,
    ICastleSiegeService castleSiege,
    IBowlEventService bowl,
    ScheduleClock scheduleClock,
    IEventStartRules startRules,
    IOptions<GameServerSettings> settings) : IEventSchedulePacketCoordinator
{
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = 24 * MinutesPerHour;
    private const int DaysPerWeek = 7;
    private const int MinutesPerWeek = DaysPerWeek * MinutesPerDay;
    private const byte AnyLevel = 0;
    private const byte NoCode = 0;
    private const int DaysAhead = DaysPerWeek - 1;
    private const byte FirstSiegeWeekday = 1;

    public const string KrowazName = "Krowaz's Dominion";
    public const string BifrostName = "Bifrost";
    public const string CastleSiegeName = "Castle Siege War";
    public const string BowlName = "Bowl Event";
    public const string NationWarName = "National War";
    public const string SnowWarName = "Snow War";
    public const string WantedName = "Wanted";
    private const string BifrostDescription = "Bifrost in Ronark Land";
    private const string CastleSiegeDescription = "Clan war for Delos Castle";

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1 || packet.ReadByte() != EventSchedulePacketWriter.SubSchedule)
            return;

        await session.Client.SendPacket(Current());
    }

    public Packet Current() => EventSchedulePacketWriter.Schedule(
        (int)Math.Clamp(scheduleClock.Offset.TotalSeconds, int.MinValue, int.MaxValue), Now(), Events(DateTime.UtcNow));

    internal List<EventNow> Now()
    {
        var now = new List<EventNow>();
        foreach (var run in scheduler.TempleEvents)
            now.Add(new EventNow(EventScheduleKind.Temple, (byte)run.Contest, TempleEventRules.NameFor(run.Contest),
                run.JoinOpen ? EventSchedulePhase.SignUp : EventSchedulePhase.Running,
                run.SecondsLeft, run.MinLevel, run.MaxLevel));

        foreach (var race in collectionRaces.ActiveRaces)
            now.Add(new EventNow(EventScheduleKind.CollectionRace, NoCode, race.Race.Name, EventSchedulePhase.Running,
                race.RemainingSeconds, race.Race.MinLevel, race.Race.MaxLevel));

        if (lottery.ActiveEvent is { Closed: false } draw)
            now.Add(new EventNow(EventScheduleKind.Lottery, NoCode, draw.EventData.Name, EventSchedulePhase.Running,
                draw.RemainingSeconds, AnyLevel, AnyLevel));

        if (krowaz.IsOpen)
            now.Add(new EventNow(EventScheduleKind.Krowaz, NoCode, KrowazName, EventSchedulePhase.Running,
                krowaz.RemainingSeconds, KrowazService.EntryMinLevel, AnyLevel));

        if (bifrost.IsActive)
            now.Add(new EventNow(EventScheduleKind.Bifrost, NoCode, BifrostName, EventSchedulePhase.Running,
                (int)Math.Min(bifrost.RemainingSecs, int.MaxValue), AnyLevel, AnyLevel));

        if (bowl.IsActive)
            now.Add(new EventNow(EventScheduleKind.Bowl, NoCode, BowlName, EventSchedulePhase.Running,
                (int)bowl.Remaining.TotalSeconds, bowl.Settings.MinLevel, AnyLevel));

        if (castleSiege.Phase == CastleSiegePhase.War)
            now.Add(new EventNow(EventScheduleKind.CastleSiege, NoCode, CastleSiegeName, EventSchedulePhase.Running,
                castleSiege.WarSecondsLeft, AnyLevel, AnyLevel));

        return now;
    }

    internal List<ScheduledEvent> Events(DateTime utcNow)
    {
        var events = settings.Value.Events;
        int offset = ScheduleTime.OffsetMinutes(events, utcNow);
        var scheduleToday = DateOnly.FromDateTime(utcNow.AddMinutes(offset));
        var utcToday = DateOnly.FromDateTime(utcNow);
        var list = new List<ScheduledEvent>();

        var templeGroups = (gameDataService.TempleEventSchedules ?? [])
            .OrderBy(s => s.Id)
            .GroupBy(s => (s.Event,
                Min: s.MinLevel > 0 ? s.MinLevel : TempleEventRules.DefaultMinLevel(s.Event),
                Max: s.MaxLevel > 0 ? s.MaxLevel : TempleEventRules.DefaultMaxLevel(s.Event)));
        foreach (var group in templeGroups)
            list.Add(new ScheduledEvent(EventScheduleKind.Temple, TempleEventRules.NameFor(group.Key.Event),
                TempleEventRules.DescriptionFor(group.Key.Event), group.Key.Min, group.Key.Max,
                Slots(group, offset, scheduleToday)));

        if (Slots(startRules.For(EventScheduleKind.Krowaz, events.KrowazStartHours), offset, scheduleToday) is { Count: > 0 } krowazSlots)
            list.Add(new ScheduledEvent(EventScheduleKind.Krowaz, KrowazName,
                $"Opens for {EventStartRules.MinutesOf(startRules, EventScheduleKind.Krowaz, KrowazService.OpenMinutes)} minutes", KrowazService.EntryMinLevel, AnyLevel, krowazSlots));

        if (Slots(startRules.For(EventScheduleKind.Bifrost, events.BifrostStartHours), offset, scheduleToday) is { Count: > 0 } bifrostSlots)
            list.Add(new ScheduledEvent(EventScheduleKind.Bifrost, BifrostName, BifrostDescription, AnyLevel, AnyLevel, bifrostSlots));

        var bowlSettings = bowl.Settings;
        if (bowlSettings.Enabled
            && Slots(startRules.For(EventScheduleKind.Bowl, bowlSettings.StartHours), offset, scheduleToday) is { Count: > 0 } bowlSlots)
            list.Add(new ScheduledEvent(EventScheduleKind.Bowl, BowlName,
                $"Rewards for staying active in the Bowl of Ronark Land, {EventStartRules.MinutesOf(startRules, EventScheduleKind.Bowl, bowlSettings.DefaultDurationMinutes)} minutes",
                bowlSettings.MinLevel, AnyLevel, bowlSlots));

        foreach (var (kind, name, description) in new[]
        {
            (EventScheduleKind.NationWar, NationWarName, $"War in one of the battle zones, {EventStartRules.MinutesOf(startRules, EventScheduleKind.NationWar, events.BattleDurationMinutes)} minutes"),
            (EventScheduleKind.SnowWar, SnowWarName, $"Snow battle, {EventStartRules.MinutesOf(startRules, EventScheduleKind.SnowWar, events.BattleDurationMinutes)} minutes"),
            (EventScheduleKind.Wanted, WantedName, $"Wanted in Ronark Land and Ronark Land Base, {EventStartRules.MinutesOf(startRules, EventScheduleKind.Wanted, events.WantedMinutes)} minutes"),
        })
            if (Slots(startRules.For(kind, []), offset, scheduleToday) is { Count: > 0 } slots)
                list.Add(new ScheduledEvent(kind, name, description, AnyLevel, AnyLevel, slots));

        if (gameDataService.SiegeWarfare is { } siege && SiegeRules.IsWeekday(siege.WarDay))
            list.Add(new ScheduledEvent(EventScheduleKind.CastleSiege, CastleSiegeName, CastleSiegeDescription,
                AnyLevel, AnyLevel,
                [Slot((DayOfWeek)(siege.WarDay - FirstSiegeWeekday), siege.WarTime, siege.WarMinute, offset)]));

        foreach (var group in gameDataService.CollectionRaceSchedulesByRace)
        {
            if (!gameDataService.CollectionRaceTable.TryGetValue(group.Key, out var race))
                continue;
            list.Add(new ScheduledEvent(EventScheduleKind.CollectionRace, race.Name,
                $"Collection Race, {race.DurationMinutes} minutes, first {race.MaxWinners} win", race.MinLevel, race.MaxLevel,
                Slots(group, 0, utcToday)));
        }

        foreach (var group in gameDataService.LotterySchedulesByEvent)
        {
            if (!gameDataService.LotteryEventTable.TryGetValue(group.Key, out var draw))
                continue;
            list.Add(new ScheduledEvent(EventScheduleKind.Lottery, draw.Name,
                $"Lottery draw, {draw.DurationMinutes} minutes", AnyLevel, AnyLevel,
                Slots(group, 0, utcToday)));
        }

        return list;
    }

    internal static List<EventSlot> Slots(IEnumerable<IScheduleRule> rules, int offsetMinutes, DateOnly today) =>
        rules.SelectMany(rule => rule.Repeat switch
        {
            ScheduleRepeat.Daily => [Slot(null, rule.Hour, rule.Minute, offsetMinutes)],
            ScheduleRepeat.Weekly => [Slot(rule.Day, rule.Hour, rule.Minute, offsetMinutes)],
            _ => rule.Occurrences(today, today.AddDays(DaysAhead)).Select(at => Slot(at.DayOfWeek, rule.Hour, rule.Minute, offsetMinutes)),
        }).ToList();

    internal static EventSlot Slot(DayOfWeek? day, byte hour, byte minute, int offsetMinutes)
    {
        if (day == null)
        {
            int minuteOfDay = Wrap(hour * MinutesPerHour + minute - offsetMinutes, MinutesPerDay);
            return new EventSlot(EventSchedulePacketWriter.EveryDay,
                (byte)(minuteOfDay / MinutesPerHour), (byte)(minuteOfDay % MinutesPerHour));
        }

        int minuteOfWeek = Wrap((int)day.Value * MinutesPerDay + hour * MinutesPerHour + minute - offsetMinutes,
            MinutesPerWeek);
        int dayMinutes = minuteOfWeek % MinutesPerDay;
        return new EventSlot((byte)(minuteOfWeek / MinutesPerDay),
            (byte)(dayMinutes / MinutesPerHour), (byte)(dayMinutes % MinutesPerHour));
    }

    private static int Wrap(int value, int span) => ((value % span) + span) % span;
}
