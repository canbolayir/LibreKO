using LibreKO.Common.Enums;

namespace LibreKO.Common.Domain.Entities.GameData;

public interface IScheduleRule
{
    ScheduleRepeat Repeat { get; }
    DateOnly? Date { get; }
    DayOfWeek? Day { get; }
    byte DayOfMonth { get; }
    byte Hour { get; }
    byte Minute { get; }
}

public static class ScheduleRule
{
    public const int HoursPerDay = 24;
    public const int MinutesPerHour = 60;
    public const int LastDayOfMonth = 31;

    public static bool Matches(this IScheduleRule rule, DateTime clock) =>
        rule.Hour == clock.Hour && rule.Minute == clock.Minute && OnDay(rule, DateOnly.FromDateTime(clock));

    public static bool OnDay(this IScheduleRule rule, DateOnly day) => rule.Repeat switch
    {
        ScheduleRepeat.Daily => true,
        ScheduleRepeat.Weekly => rule.Day == day.DayOfWeek,
        ScheduleRepeat.Once => rule.Date == day,
        ScheduleRepeat.Monthly => Math.Min(rule.DayOfMonth, DateTime.DaysInMonth(day.Year, day.Month)) == day.Day,
        _ => false,
    };

    public static IEnumerable<DateTime> Occurrences(this IScheduleRule rule, DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
            if (rule.OnDay(day))
                yield return day.ToDateTime(new TimeOnly(rule.Hour, rule.Minute));
    }

    public static string? Problem(ScheduleRepeat repeat, DateOnly? date, DayOfWeek? day, byte dayOfMonth, byte hour, byte minute)
    {
        if (hour >= HoursPerDay || minute >= MinutesPerHour)
            return "The time must be 00:00 to 23:59.";
        return repeat switch
        {
            ScheduleRepeat.Daily => null,
            ScheduleRepeat.Weekly => day == null ? "A weekly start needs a weekday." : null,
            ScheduleRepeat.Once => date == null ? "A one-time start needs a date." : null,
            ScheduleRepeat.Monthly => dayOfMonth is < 1 or > LastDayOfMonth ? "A monthly start needs a day of the month, 1 to 31." : null,
            _ => "Unknown repeat.",
        };
    }

    public static DayOfWeek? WeekdayFor(ScheduleRepeat repeat, DayOfWeek? day) => repeat == ScheduleRepeat.Weekly ? day : null;
}
