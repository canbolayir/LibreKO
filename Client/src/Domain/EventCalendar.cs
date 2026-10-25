using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public enum ScheduleRepeat : byte { Daily = 0, Weekly = 1, Once = 2, Monthly = 3 }

public enum EventRuleClock : byte { Schedule = 0, Utc = 1 }

public sealed record EventRule(int Id, ScheduleRepeat Repeat, DateOnly? Date, DayOfWeek? Day, int DayOfMonth, int Hour, int Minute,
    int MinLevel, int MaxLevel, int CountdownMinutes);

public sealed record EventOwner(EventKind Kind, int Id, string Name, EventRuleClock Clock, bool Editable, bool FromSettings, bool HasLevels,
    IReadOnlyList<EventRule> Rules, int DurationMinutes = 0);

public readonly record struct EventStart(DateTime Local, EventOwner Owner, EventRule Rule);

public enum CalendarView { Month, Week, Day }

public static class EventCalendar
{
    public const int GridDays = 42;
    public const int DaysPerWeek = 7;
    private const int YearScale = 10_000;
    private const int MonthScale = 100;
    private const int ShortNameLength = 3;

    public static readonly DayOfWeek[] WeekOrder =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static string ShortName(DayOfWeek day) => day.ToString()[..ShortNameLength];

    public static bool OnDay(EventRule rule, DateOnly day) => rule.Repeat switch
    {
        ScheduleRepeat.Daily => true,
        ScheduleRepeat.Weekly => rule.Day == day.DayOfWeek,
        ScheduleRepeat.Once => rule.Date == day,
        ScheduleRepeat.Monthly => Math.Min(rule.DayOfMonth, DateTime.DaysInMonth(day.Year, day.Month)) == day.Day,
        _ => false,
    };

    public static DateOnly WeekStart(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + DaysPerWeek - (int)DayOfWeek.Monday) % DaysPerWeek));

    public static (DateOnly First, DateOnly Last) Range(CalendarView view, DateOnly day) => view switch
    {
        CalendarView.Day => (day, day),
        CalendarView.Week => (WeekStart(day), WeekStart(day).AddDays(DaysPerWeek - 1)),
        _ => (GridStart(day.Year, day.Month), GridStart(day.Year, day.Month).AddDays(GridDays - 1)),
    };

    public static DateOnly Step(CalendarView view, DateOnly day, int direction) => view switch
    {
        CalendarView.Day => day.AddDays(direction),
        CalendarView.Week => day.AddDays(direction * DaysPerWeek),
        _ => new DateOnly(day.Year, day.Month, 1).AddMonths(direction),
    };

    public static string Title(CalendarView view, DateOnly day)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (view == CalendarView.Day) return day.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM yyyy", culture);
        if (view == CalendarView.Week)
        {
            var (first, last) = Range(view, day);
            return first.Month == last.Month
                ? $"{first.Day} - {last.Day} {last.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture)}"
                : $"{first.ToDateTime(TimeOnly.MinValue).ToString("d MMM", culture)} - {last.ToDateTime(TimeOnly.MinValue).ToString("d MMM yyyy", culture)}";
        }
        return day.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture);
    }

    public static DateOnly GridStart(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        int back = ((int)first.DayOfWeek + DaysPerWeek - (int)DayOfWeek.Monday) % DaysPerWeek;
        return first.AddDays(-back);
    }

    public static List<EventStart> Starts(IEnumerable<EventOwner> owners, DateOnly fromLocal, DateOnly toLocal, TimeZoneInfo? scheduleZone,
        int scheduleOffsetMinutes, TimeZoneInfo local)
    {
        var starts = new List<EventStart>();
        foreach (var owner in owners)
            foreach (var rule in owner.Rules)
                for (var day = fromLocal.AddDays(-1); day <= toLocal.AddDays(1); day = day.AddDays(1))
                {
                    if (!OnDay(rule, day)) continue;
                    var at = Local(day.ToDateTime(new TimeOnly(rule.Hour, rule.Minute)), owner.Clock, scheduleZone, scheduleOffsetMinutes, local);
                    var shown = DateOnly.FromDateTime(at);
                    if (shown >= fromLocal && shown <= toLocal) starts.Add(new EventStart(at, owner, rule));
                }
        starts.Sort((a, b) => a.Local.CompareTo(b.Local));
        return starts;
    }

    public static DateTime Local(DateTime clockTime, EventRuleClock clock, TimeZoneInfo? scheduleZone, int scheduleOffsetMinutes, TimeZoneInfo local)
    {
        var unspecified = DateTime.SpecifyKind(clockTime, DateTimeKind.Unspecified);
        DateTime utc = clock == EventRuleClock.Utc
            ? DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)
            : scheduleZone != null
                ? TimeZoneInfo.ConvertTimeToUtc(scheduleZone.IsInvalidTime(unspecified) ? unspecified.AddHours(1) : unspecified, scheduleZone)
                : DateTime.SpecifyKind(unspecified.AddMinutes(-scheduleOffsetMinutes), DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, local);
    }

    public static TimeZoneInfo? FindZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    public static int DateNumber(DateOnly date) => date.Year * YearScale + date.Month * MonthScale + date.Day;

    public static DateOnly? DateFromNumber(int number)
    {
        if (number <= 0) return null;
        try { return new DateOnly(number / YearScale, number / MonthScale % MonthScale, number % MonthScale); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    public static string RepeatName(ScheduleRepeat repeat) => repeat switch
    {
        ScheduleRepeat.Once => "Once",
        ScheduleRepeat.Daily => "Every day",
        ScheduleRepeat.Weekly => "Every week",
        ScheduleRepeat.Monthly => "Every month",
        _ => repeat.ToString(),
    };

    public static string Summary(IReadOnlyList<EventRule> rules, int shown)
    {
        if (rules.Count == 0) return "No starts";
        string text = string.Join(" · ", rules.Take(shown).Select(Describe));
        return rules.Count > shown ? $"{text} · +{rules.Count - shown}" : text;
    }

    public static string Describe(EventRule rule) => rule.Repeat switch
    {
        ScheduleRepeat.Daily => $"Every day {rule.Hour:00}:{rule.Minute:00}",
        ScheduleRepeat.Weekly => $"Every {rule.Day} {rule.Hour:00}:{rule.Minute:00}",
        ScheduleRepeat.Once => $"{rule.Date:yyyy-MM-dd} {rule.Hour:00}:{rule.Minute:00}",
        ScheduleRepeat.Monthly => $"Day {rule.DayOfMonth} of each month {rule.Hour:00}:{rule.Minute:00}",
        _ => "",
    };
}
