using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface IAdminEventContentEditor
{
    bool Handles(byte sub);
    Task HandleAsync(UserSession session, byte sub, Packet packet);
}

public sealed class AdminEventContentEditor(
    IGameDataService gameData,
    IEventContentStore store,
    IAdminEventScheduleEditor schedules,
    ILotteryService lottery,
    ICollectionRaceService collectionRaces,
    ILogger<AdminEventContentEditor> logger) : IAdminEventContentEditor
{
    public const byte ReqLottery = 34;
    public const byte ReqLotterySave = 35;
    public const byte ReqLotteryDelete = 36;
    public const byte ReqRace = 37;
    public const byte ReqRaceSave = 38;
    public const byte ReqRaceDelete = 39;
    public const byte AckLottery = 0x1C;
    public const byte AckRace = 0x1D;

    public const int MaxNameLength = 50;
    public const int MaxDurationMinutes = 1440;
    public const int MaxUserLimit = 10_000;
    public const int MaxWinners = 1_000;
    public const int MaxRows = 10;
    public const int MaxCount = 9_999;
    public const byte MaxRate = CollectionRaceRewardData.CertainRate;

    public bool Handles(byte sub) => sub is >= ReqLottery and <= ReqRaceDelete;

    public Task HandleAsync(UserSession session, byte sub, Packet packet) => sub switch
    {
        ReqLottery when packet.RemainingBytes >= 4 => SendLotteryAsync(session, packet.ReadInt()),
        ReqLotterySave => HandleLotterySaveAsync(session, packet),
        ReqLotteryDelete when packet.RemainingBytes >= 4 => HandleLotteryDeleteAsync(session, packet.ReadInt()),
        ReqRace when packet.RemainingBytes >= 4 => SendRaceAsync(session, packet.ReadInt()),
        ReqRaceSave => HandleRaceSaveAsync(session, packet),
        ReqRaceDelete when packet.RemainingBytes >= 4 => HandleRaceDeleteAsync(session, packet.ReadInt()),
        _ => Task.CompletedTask,
    };

    private async Task HandleLotterySaveAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 4 + 1 + 4 * 4 + 1 + 1)
            return;

        var draw = new LotteryEventData
        {
            Id = packet.ReadInt(),
            Name = packet.ReadSByteString().Trim(),
            DurationMinutes = packet.ReadInt(),
            UserLimit = packet.ReadInt(),
            ReqItemId = packet.ReadInt(),
            ReqItemCount = packet.ReadInt(),
            AutoStart = packet.ReadByte() == AdminPanelPacketWriter.Granted,
        };
        int count = packet.ReadByte();
        var rewards = new List<LotteryRewardData>();
        for (var i = 0; i < count && packet.RemainingBytes >= 12; i++)
            rewards.Add(new LotteryRewardData { Place = packet.ReadInt(), ItemId = packet.ReadInt(), Count = packet.ReadInt() });

        var problem = NameProblem(draw.Name)
            ?? (draw.Id > 0 && !gameData.LotteryEventTable.ContainsKey(draw.Id) ? $"No lottery {draw.Id}." : null)
            ?? (draw.DurationMinutes is < 1 or > MaxDurationMinutes ? $"The draw lasts 1 to {MaxDurationMinutes} minutes." : null)
            ?? (draw.UserLimit is < 1 or > MaxUserLimit ? $"The ticket limit is 1 to {MaxUserLimit}." : null)
            ?? (draw.ReqItemId != InventoryConstants.ItemGold && gameData.GetItem(draw.ReqItemId) == null ? $"No item {draw.ReqItemId} for the ticket." : null)
            ?? (draw.ReqItemCount < 1 ? "A ticket costs at least 1." : null)
            ?? (count > MaxRows || rewards.Count != count ? $"A lottery gives at most {MaxRows} rewards." : null)
            ?? rewards.Select(reward => reward.Place < 1 ? "A reward's place is 1 or more."
                : ItemProblem(reward.ItemId) ?? (reward.Count is < 1 or > MaxCount ? $"A reward is 1 to {MaxCount} items." : null))
                .FirstOrDefault(found => found != null);
        if (problem != null)
        {
            await SendResultAsync(session, false, problem);
            return;
        }

        var id = await store.SaveLotteryAsync(draw, rewards);
        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} saved lottery {Id} ({Lottery})", session.Name, id, draw.Name);
        await SendResultAsync(session, true, Running(id, isLottery: true) ? $"{draw.Name} saved; the draw running now keeps its old settings." : $"{draw.Name} saved.");
        await SendLotteryAsync(session, id);
        await schedules.SendSchedulesAsync(session);
    }

    private async Task HandleLotteryDeleteAsync(UserSession session, int id)
    {
        if (Running(id, isLottery: true))
        {
            await SendResultAsync(session, false, "Close the running draw first.");
            return;
        }

        if (!await store.DeleteLotteryAsync(id))
        {
            await SendResultAsync(session, false, $"No lottery {id}.");
            return;
        }

        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} deleted lottery {Id}", session.Name, id);
        await SendResultAsync(session, true, $"Lottery {id} deleted with its rewards and starts.");
        await schedules.SendSchedulesAsync(session);
    }

    private async Task HandleRaceSaveAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 4 + 1 + 3 + 8 + 1 + 1)
            return;

        var race = new CollectionRaceData
        {
            Id = packet.ReadInt(),
            Name = packet.ReadSByteString().Trim(),
            ZoneId = packet.ReadByte(),
            MinLevel = packet.ReadByte(),
            MaxLevel = packet.ReadByte(),
            DurationMinutes = packet.ReadInt(),
            MaxWinners = packet.ReadInt(),
            AutoStart = packet.ReadByte() == AdminPanelPacketWriter.Granted,
        };
        int objectiveCount = packet.ReadByte();
        var objectives = new List<CollectionRaceObjectiveData>();
        for (var i = 0; i < objectiveCount && packet.RemainingBytes >= 9; i++)
            objectives.Add(new CollectionRaceObjectiveData { Kind = (CollectionRaceObjectiveKind)packet.ReadByte(), TargetId = packet.ReadInt(), Count = packet.ReadInt() });
        int rewardCount = packet.RemainingBytes >= 1 ? packet.ReadByte() : 0;
        var rewards = new List<CollectionRaceRewardData>();
        for (var i = 0; i < rewardCount && packet.RemainingBytes >= 9; i++)
            rewards.Add(new CollectionRaceRewardData { ItemId = packet.ReadInt(), ItemCount = packet.ReadInt(), Rate = packet.ReadByte() });

        var problem = NameProblem(race.Name)
            ?? (race.Id > 0 && !gameData.CollectionRaceTable.ContainsKey(race.Id) ? $"No Collection Race {race.Id}." : null)
            ?? (!gameData.ZoneInfoTable.ContainsKey(race.ZoneId) ? $"Zone {race.ZoneId} is not on this server." : null)
            ?? (race.MinLevel < 1 || race.MaxLevel < race.MinLevel ? "The levels must be 1 or more, the lowest first." : null)
            ?? (race.DurationMinutes is < 1 or > MaxDurationMinutes ? $"The race lasts 1 to {MaxDurationMinutes} minutes." : null)
            ?? (race.MaxWinners is < 1 or > MaxWinners ? $"1 to {MaxWinners} winners." : null)
            ?? (objectiveCount is < 1 or > MaxRows || objectives.Count != objectiveCount ? $"A race has 1 to {MaxRows} targets." : null)
            ?? (rewardCount > MaxRows || rewards.Count != rewardCount ? $"A race gives at most {MaxRows} rewards." : null)
            ?? objectives.Select(ObjectiveProblem).FirstOrDefault(found => found != null)
            ?? rewards.Select(reward => ItemProblem(reward.ItemId)
                ?? (reward.ItemCount is < 1 or > MaxCount ? $"A reward is 1 to {MaxCount} items." : null)
                ?? (reward.Rate is < 1 or > MaxRate ? $"A reward's chance is 1 to {MaxRate} percent." : null))
                .FirstOrDefault(found => found != null);
        if (problem != null)
        {
            await SendResultAsync(session, false, problem);
            return;
        }

        var id = await store.SaveRaceAsync(race, objectives, rewards);
        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} saved Collection Race {Id} ({Race})", session.Name, id, race.Name);
        await SendResultAsync(session, true, Running(id, isLottery: false) ? $"{race.Name} saved; the race running now keeps its old settings." : $"{race.Name} saved.");
        await SendRaceAsync(session, id);
        await schedules.SendSchedulesAsync(session);
    }

    private async Task HandleRaceDeleteAsync(UserSession session, int id)
    {
        if (Running(id, isLottery: false))
        {
            await SendResultAsync(session, false, "Close the running race first.");
            return;
        }

        if (!await store.DeleteRaceAsync(id))
        {
            await SendResultAsync(session, false, $"No Collection Race {id}.");
            return;
        }

        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} deleted Collection Race {Id}", session.Name, id);
        await SendResultAsync(session, true, $"Collection Race {id} deleted with its targets, rewards and starts.");
        await schedules.SendSchedulesAsync(session);
    }

    private static string? NameProblem(string name) =>
        name.Length is < 1 or > MaxNameLength ? $"The name is 1 to {MaxNameLength} letters." : null;

    private string? ItemProblem(int itemId) => gameData.GetItem(itemId) == null ? $"No item {itemId}." : null;

    private string? ObjectiveProblem(CollectionRaceObjectiveData objective)
    {
        if (objective.Count is < 1 or > MaxCount)
            return $"A target counts 1 to {MaxCount}.";
        return objective.Kind switch
        {
            CollectionRaceObjectiveKind.Monster => gameData.MonsterTable.ContainsKey(objective.TargetId) ? null : $"No monster {objective.TargetId}.",
            CollectionRaceObjectiveKind.Item => ItemProblem(objective.TargetId),
            CollectionRaceObjectiveKind.EnemyPlayer => null,
            _ => "Unknown target kind.",
        };
    }

    private bool Running(int id, bool isLottery) => isLottery
        ? lottery.ActiveEvent is { Closed: false } draw && draw.EventData.Id == id
        : collectionRaces.ActiveRaces.Any(active => active.Race.Id == id);

    private Task SendLotteryAsync(UserSession session, int id)
    {
        if (!gameData.LotteryEventTable.TryGetValue(id, out var draw))
            return SendResultAsync(session, false, $"No lottery {id}.");

        var packet = Sub(AckLottery);
        packet.WriteInt(draw.Id);
        packet.WriteSByteString(draw.Name);
        packet.WriteInt(draw.DurationMinutes);
        packet.WriteInt(draw.UserLimit);
        packet.WriteInt(draw.ReqItemId);
        packet.WriteInt(draw.ReqItemCount);
        packet.WriteByte(Flag(draw.AutoStart));
        packet.WriteByte(Flag(Running(id, isLottery: true)));
        var rewards = gameData.LotteryRewardsByEvent[id].OrderBy(reward => reward.Place).ToList();
        packet.WriteByte((byte)rewards.Count);
        foreach (var reward in rewards)
        {
            packet.WriteInt(reward.Place);
            packet.WriteInt(reward.ItemId);
            packet.WriteInt(reward.Count);
        }

        return session.Client.SendPacket(packet);
    }

    private Task SendRaceAsync(UserSession session, int id)
    {
        if (!gameData.CollectionRaceTable.TryGetValue(id, out var race))
            return SendResultAsync(session, false, $"No Collection Race {id}.");

        var packet = Sub(AckRace);
        packet.WriteInt(race.Id);
        packet.WriteSByteString(race.Name);
        packet.WriteByte(race.ZoneId);
        packet.WriteByte(race.MinLevel);
        packet.WriteByte(race.MaxLevel);
        packet.WriteInt(race.DurationMinutes);
        packet.WriteInt(race.MaxWinners);
        packet.WriteByte(Flag(race.AutoStart));
        packet.WriteByte(Flag(Running(id, isLottery: false)));
        var objectives = gameData.CollectionRaceObjectivesByRace[id].OrderBy(objective => objective.Ordinal).ToList();
        packet.WriteByte((byte)objectives.Count);
        foreach (var objective in objectives)
        {
            packet.WriteByte((byte)objective.Kind);
            packet.WriteInt(objective.TargetId);
            packet.WriteInt(objective.Count);
            packet.WriteSByteString(TargetName(objective));
        }

        var rewards = gameData.CollectionRaceRewardsByRace[id].ToList();
        packet.WriteByte((byte)rewards.Count);
        foreach (var reward in rewards)
        {
            packet.WriteInt(reward.ItemId);
            packet.WriteInt(reward.ItemCount);
            packet.WriteByte(reward.Rate);
        }

        return session.Client.SendPacket(packet);
    }

    private string TargetName(CollectionRaceObjectiveData objective) => objective.Kind switch
    {
        CollectionRaceObjectiveKind.Monster => gameData.MonsterTable.TryGetValue(objective.TargetId, out var monster) ? monster.Name : $"monster {objective.TargetId}",
        CollectionRaceObjectiveKind.Item => gameData.GetItem(objective.TargetId)?.Name ?? $"item {objective.TargetId}",
        _ => "enemy players",
    };

    private static byte Flag(bool on) => on ? AdminPanelPacketWriter.Granted : AdminPanelPacketWriter.Denied;

    private static Packet Sub(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteByte(sub);
        return packet;
    }

    private static Task SendResultAsync(UserSession session, bool ok, string message) =>
        session.Client.SendPacket(AdminPanelPacketWriter.Result(ok, message));
}
