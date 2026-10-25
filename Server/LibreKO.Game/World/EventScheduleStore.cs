using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol.Writers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.World;

public sealed record RuleFields(ScheduleRepeat Repeat, DateOnly? Date, DayOfWeek? Day, byte DayOfMonth, byte Hour, byte Minute);

public sealed record TempleRuleFields(byte MinLevel, byte MaxLevel, byte CountdownMinutes);

public interface IEventScheduleStore
{
    Task<bool> SaveAsync(EventScheduleKind kind, int owner, int ruleId, RuleFields rule, TempleRuleFields temple);
    Task<bool> DeleteAsync(EventScheduleKind kind, int owner, int ruleId);
    Task CopyHoursAsync(EventScheduleKind kind, IEnumerable<int> hours);
    Task SaveDurationAsync(EventScheduleKind kind, int minutes);
}

public sealed class EventScheduleStore(IServiceScopeFactory scopeFactory) : IEventScheduleStore
{
    public async Task<bool> SaveAsync(EventScheduleKind kind, int owner, int ruleId, RuleFields rule, TempleRuleFields temple)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        switch (kind)
        {
            case EventScheduleKind.Temple:
                var templeRow = ruleId > 0
                    ? await db.TempleEventSchedules.FirstOrDefaultAsync(row => row.Id == ruleId && row.Event == (TempleEvent)owner)
                    : Added(db.TempleEventSchedules, new TempleEventScheduleData { Id = await NextIdAsync(db.TempleEventSchedules.Select(row => row.Id)), Event = (TempleEvent)owner });
                if (templeRow == null)
                    return false;
                (templeRow.Repeat, templeRow.Date, templeRow.Day, templeRow.DayOfMonth, templeRow.Hour, templeRow.Minute) = Unpack(rule);
                (templeRow.MinLevel, templeRow.MaxLevel, templeRow.CountdownMinutes) = (temple.MinLevel, temple.MaxLevel, temple.CountdownMinutes);
                break;
            case EventScheduleKind.CollectionRace:
                var raceRow = ruleId > 0
                    ? await db.CollectionRaceSchedules.FirstOrDefaultAsync(row => row.Id == ruleId && row.RaceId == owner)
                    : Added(db.CollectionRaceSchedules, new CollectionRaceScheduleData { Id = await NextIdAsync(db.CollectionRaceSchedules.Select(row => row.Id)), RaceId = owner });
                if (raceRow == null)
                    return false;
                (raceRow.Repeat, raceRow.Date, raceRow.Day, raceRow.DayOfMonth, raceRow.Hour, raceRow.Minute) = Unpack(rule);
                break;
            case EventScheduleKind.Lottery:
                var lotteryRow = ruleId > 0
                    ? await db.LotterySchedules.FirstOrDefaultAsync(row => row.Id == ruleId && row.LotteryId == owner)
                    : Added(db.LotterySchedules, new LotteryScheduleData { Id = await NextIdAsync(db.LotterySchedules.Select(row => row.Id)), LotteryId = owner });
                if (lotteryRow == null)
                    return false;
                (lotteryRow.Repeat, lotteryRow.Date, lotteryRow.Day, lotteryRow.DayOfMonth, lotteryRow.Hour, lotteryRow.Minute) = Unpack(rule);
                break;
            default:
                var startRow = ruleId > 0
                    ? await db.EventStarts.FirstOrDefaultAsync(row => row.Id == ruleId && row.Kind == (byte)kind)
                    : Added(db.EventStarts, new EventStartData { Id = await NextIdAsync(db.EventStarts.Select(row => row.Id)), Kind = (byte)kind });
                if (startRow == null)
                    return false;
                (startRow.Repeat, startRow.Date, startRow.Day, startRow.DayOfMonth, startRow.Hour, startRow.Minute) = Unpack(rule);
                break;
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(EventScheduleKind kind, int owner, int ruleId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        object? row = kind switch
        {
            EventScheduleKind.Temple => await db.TempleEventSchedules.FirstOrDefaultAsync(r => r.Id == ruleId && r.Event == (TempleEvent)owner),
            EventScheduleKind.CollectionRace => await db.CollectionRaceSchedules.FirstOrDefaultAsync(r => r.Id == ruleId && r.RaceId == owner),
            EventScheduleKind.Lottery => await db.LotterySchedules.FirstOrDefaultAsync(r => r.Id == ruleId && r.LotteryId == owner),
            _ => await db.EventStarts.FirstOrDefaultAsync(r => r.Id == ruleId && r.Kind == (byte)kind),
        };
        if (row == null)
            return false;

        db.Remove(row);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task CopyHoursAsync(EventScheduleKind kind, IEnumerable<int> hours)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.EventStarts.AnyAsync(row => row.Kind == (byte)kind))
            return;

        var id = await NextIdAsync(db.EventStarts.Select(row => row.Id));
        foreach (var start in EventStartRules.FromHours(hours))
            db.EventStarts.Add(new EventStartData { Id = id++, Kind = (byte)kind, Repeat = ScheduleRepeat.Daily, Hour = start.Hour, Minute = start.Minute });
        await db.SaveChangesAsync();
    }

    public async Task SaveDurationAsync(EventScheduleKind kind, int minutes)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.EventDurations.FirstOrDefaultAsync(duration => duration.Kind == (byte)kind)
            ?? Added(db.EventDurations, new EventDurationData { Kind = (byte)kind });
        row.Minutes = minutes;
        await db.SaveChangesAsync();
    }

    private static T Added<T>(DbSet<T> set, T row) where T : class
    {
        set.Add(row);
        return row;
    }

    private static async Task<int> NextIdAsync(IQueryable<int> ids) => (await ids.DefaultIfEmpty().MaxAsync()) + 1;

    private static (ScheduleRepeat, DateOnly?, DayOfWeek?, byte, byte, byte) Unpack(RuleFields rule) =>
        (rule.Repeat, rule.Repeat == ScheduleRepeat.Once ? rule.Date : null, ScheduleRule.WeekdayFor(rule.Repeat, rule.Day),
            rule.Repeat == ScheduleRepeat.Monthly ? rule.DayOfMonth : (byte)0, rule.Hour, rule.Minute);
}
