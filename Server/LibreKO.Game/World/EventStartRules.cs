using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.World;

public interface IEventStartRules
{
    IReadOnlyList<IScheduleRule> For(EventScheduleKind kind, IEnumerable<int> configHours);
    int MinutesFor(EventScheduleKind kind, int settingsMinutes);
}

public sealed record HourlyStart(byte Hour) : IScheduleRule
{
    public ScheduleRepeat Repeat => ScheduleRepeat.Daily;
    public DateOnly? Date => null;
    public DayOfWeek? Day => null;
    public byte DayOfMonth => 0;
    public byte Minute => 0;
}

public sealed class EventStartRules(IGameDataService gameData) : IEventStartRules
{
    public IReadOnlyList<IScheduleRule> For(EventScheduleKind kind, IEnumerable<int> configHours)
    {
        var rows = gameData.EventStarts.Where(start => start.Kind == (byte)kind).ToList<IScheduleRule>();
        return rows.Count > 0 ? rows : FromHours(configHours);
    }

    public const int MaxMinutes = 1440;

    public static readonly IReadOnlySet<EventScheduleKind> TimedKinds = new HashSet<EventScheduleKind>
    {
        EventScheduleKind.Krowaz, EventScheduleKind.Bowl, EventScheduleKind.NationWar, EventScheduleKind.SnowWar, EventScheduleKind.Wanted,
    };

    public int MinutesFor(EventScheduleKind kind, int settingsMinutes) => MinutesIn(gameData, kind, settingsMinutes);

    public static int MinutesIn(IGameDataService gameData, EventScheduleKind kind, int settingsMinutes) =>
        gameData.EventDurations.FirstOrDefault(duration => duration.Kind == (byte)kind) is { Minutes: > 0 } stored
            ? stored.Minutes
            : settingsMinutes;

    public static int MinutesOf(IEventStartRules? rules, EventScheduleKind kind, int settingsMinutes) =>
        rules?.MinutesFor(kind, settingsMinutes) is > 0 and var minutes ? minutes : settingsMinutes;

    public static int SettingsMinutes(GameServerSettings settings, EventScheduleKind kind) => kind switch
    {
        EventScheduleKind.Krowaz => KrowazService.OpenMinutes,
        EventScheduleKind.Bowl => settings.BowlEvent.DefaultDurationMinutes,
        EventScheduleKind.NationWar or EventScheduleKind.SnowWar => settings.Events.BattleDurationMinutes,
        EventScheduleKind.Wanted => settings.Events.WantedMinutes,
        _ => 0,
    };

    public static IReadOnlyList<IScheduleRule> FromHours(IEnumerable<int> hours) =>
        hours.Where(hour => hour is >= 0 and < ScheduleRule.HoursPerDay).Order().Select(hour => (IScheduleRule)new HourlyStart((byte)hour)).ToList();

    public static bool StartsAt(IEventStartRules? rules, EventScheduleKind kind, IEnumerable<int> configHours, DateTime clock) =>
        (rules?.For(kind, configHours) ?? FromHours(configHours)).Any(rule => rule.Matches(clock));
}
