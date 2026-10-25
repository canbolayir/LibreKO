using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.World;

public interface IEventContentStore
{
    Task<int> SaveLotteryAsync(LotteryEventData lottery, IReadOnlyList<LotteryRewardData> rewards);
    Task<bool> DeleteLotteryAsync(int id);
    Task<int> SaveRaceAsync(CollectionRaceData race, IReadOnlyList<CollectionRaceObjectiveData> objectives, IReadOnlyList<CollectionRaceRewardData> rewards);
    Task<bool> DeleteRaceAsync(int id);
}

public sealed class EventContentStore(IServiceScopeFactory scopeFactory) : IEventContentStore
{
    public async Task<int> SaveLotteryAsync(LotteryEventData lottery, IReadOnlyList<LotteryRewardData> rewards)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = lottery.Id > 0 ? await db.LotteryEvents.FindAsync(lottery.Id) : null;
        if (stored == null)
        {
            stored = new LotteryEventData { Id = lottery.Id > 0 ? lottery.Id : await NextIdAsync(db.LotteryEvents.Select(row => row.Id)) };
            db.LotteryEvents.Add(stored);
        }

        stored.Name = lottery.Name;
        stored.DurationMinutes = lottery.DurationMinutes;
        stored.UserLimit = lottery.UserLimit;
        stored.ReqItemId = lottery.ReqItemId;
        stored.ReqItemCount = lottery.ReqItemCount;
        stored.AutoStart = lottery.AutoStart;
        db.LotteryRewards.RemoveRange(db.LotteryRewards.Where(row => row.LotteryId == stored.Id));
        var nextReward = await NextIdAsync(db.LotteryRewards.Select(row => row.Id));
        foreach (var reward in rewards)
            db.LotteryRewards.Add(new LotteryRewardData { Id = nextReward++, LotteryId = stored.Id, Place = reward.Place, ItemId = reward.ItemId, Count = reward.Count });
        await db.SaveChangesAsync();
        return stored.Id;
    }

    public async Task<bool> DeleteLotteryAsync(int id)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.LotteryEvents.FindAsync(id);
        if (stored == null)
            return false;

        db.LotteryRewards.RemoveRange(db.LotteryRewards.Where(row => row.LotteryId == id));
        db.LotterySchedules.RemoveRange(db.LotterySchedules.Where(row => row.LotteryId == id));
        db.LotteryEvents.Remove(stored);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> SaveRaceAsync(CollectionRaceData race, IReadOnlyList<CollectionRaceObjectiveData> objectives, IReadOnlyList<CollectionRaceRewardData> rewards)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = race.Id > 0 ? await db.CollectionRaces.FindAsync(race.Id) : null;
        if (stored == null)
        {
            stored = new CollectionRaceData { Id = race.Id > 0 ? race.Id : await NextIdAsync(db.CollectionRaces.Select(row => row.Id)) };
            db.CollectionRaces.Add(stored);
        }

        stored.Name = race.Name;
        stored.ZoneId = race.ZoneId;
        stored.MinLevel = race.MinLevel;
        stored.MaxLevel = race.MaxLevel;
        stored.DurationMinutes = race.DurationMinutes;
        stored.MaxWinners = race.MaxWinners;
        stored.AutoStart = race.AutoStart;
        db.CollectionRaceObjectives.RemoveRange(db.CollectionRaceObjectives.Where(row => row.RaceId == stored.Id));
        db.CollectionRaceRewards.RemoveRange(db.CollectionRaceRewards.Where(row => row.RaceId == stored.Id));
        var nextObjective = await NextIdAsync(db.CollectionRaceObjectives.Select(row => row.Id));
        byte ordinal = 0;
        foreach (var objective in objectives)
            db.CollectionRaceObjectives.Add(new CollectionRaceObjectiveData
            {
                Id = nextObjective++, RaceId = stored.Id, Ordinal = ordinal++, Kind = objective.Kind, TargetId = objective.TargetId, Count = objective.Count,
            });
        var nextReward = await NextIdAsync(db.CollectionRaceRewards.Select(row => row.Id));
        foreach (var reward in rewards)
            db.CollectionRaceRewards.Add(new CollectionRaceRewardData
            {
                Id = nextReward++, RaceId = stored.Id, ItemId = reward.ItemId, ItemCount = reward.ItemCount, Rate = reward.Rate,
            });
        await db.SaveChangesAsync();
        return stored.Id;
    }

    public async Task<bool> DeleteRaceAsync(int id)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.CollectionRaces.FindAsync(id);
        if (stored == null)
            return false;

        db.CollectionRaceObjectives.RemoveRange(db.CollectionRaceObjectives.Where(row => row.RaceId == id));
        db.CollectionRaceRewards.RemoveRange(db.CollectionRaceRewards.Where(row => row.RaceId == id));
        db.CollectionRaceSchedules.RemoveRange(db.CollectionRaceSchedules.Where(row => row.RaceId == id));
        db.CollectionRaces.Remove(stored);
        await db.SaveChangesAsync();
        return true;
    }

    private static async Task<int> NextIdAsync(IQueryable<int> ids) => (await ids.DefaultIfEmpty().MaxAsync()) + 1;
}
