using System.Collections.Concurrent;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public interface IManesSurvivalService
{
    bool HasActiveArenas { get; }
    Task StartAsync(IReadOnlyList<int> participantCharacterIds, int durationSeconds);
    Task OnPlayerKilledAsync(UserSession victim, UserSession? killer);
    Task OnMonsterKilledAsync(NpcInstance npc, UserSession killer);
    Task PickAsync(UserSession session, ManesPickAction action, short code);
    Task BonusPickAsync(UserSession session, ManesPickAction action, short code);
    void NoteInvincibleHit(UserSession attacker);
    Task DiscardAsync(UserSession session, byte bagSlot, int itemId);
    Task OnPlayerLogoutAsync(UserSession session);
    Packet BuildRanking(UserSession session);
    Task FinishAllAsync();
    Task CancelAllAsync();
    Task TickAsync();
}

public sealed class ManesSurvivalService : IManesSurvivalService
{
    private const string FullBagNotice = "Inventory is full! Could not receive the Manes' Survival reward.";

    private readonly SessionManager sessionManager;
    private readonly IGameDataService gameDataService;
    private readonly IZoneTransitionService zoneTransitionService;
    private readonly IUserNotificationService userNotificationService;
    private readonly ICombatNotificationService combatNotificationService;
    private readonly IPetService petService;
    private readonly IManesSurvivalTable table;
    private readonly INpcSummonService npcSummonService;
    private readonly INpcLifecycleService npcLifecycleService;
    private readonly ConcurrentDictionary<int, DateTime> invincibleNotices = new();
    private readonly ConcurrentDictionary<int, byte> pendingInvincibleNotices = new();
    private readonly IOptions<GameServerSettings> settings;
    private readonly ILogger<ManesSurvivalService> logger;
    private readonly TimeProvider timeProvider;
    private readonly object arenasLock = new();
    private readonly List<ManesSurvivalArena> arenas = [];

    public ManesSurvivalService(
        SessionManager sessionManager,
        IGameDataService gameDataService,
        IZoneTransitionService zoneTransitionService,
        IUserNotificationService userNotificationService,
        ICombatNotificationService combatNotificationService,
        IPetService petService,
        IManesSurvivalTable table,
        INpcSummonService npcSummonService,
        INpcLifecycleService npcLifecycleService,
        IOptions<GameServerSettings> settings,
        ILogger<ManesSurvivalService> logger,
        TimeProvider? timeProvider = null)
    {
        this.sessionManager = sessionManager;
        this.gameDataService = gameDataService;
        this.zoneTransitionService = zoneTransitionService;
        this.userNotificationService = userNotificationService;
        this.combatNotificationService = combatNotificationService;
        this.petService = petService;
        this.table = table;
        this.npcSummonService = npcSummonService;
        this.npcLifecycleService = npcLifecycleService;
        this.settings = settings;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;

        zoneTransitionService.PlayerLeavingZone += OnPlayerLeavingZoneAsync;
    }

    private ManesSurvivalSettings Rules => settings.Value.ManesSurvival;

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public bool HasActiveArenas
    {
        get
        {
            lock (arenasLock)
                return arenas.Count > 0;
        }
    }

    internal IReadOnlyList<ManesSurvivalArena> Arenas
    {
        get
        {
            lock (arenasLock)
                return arenas.ToList();
        }
    }

    public async Task StartAsync(IReadOnlyList<int> participantCharacterIds, int durationSeconds)
    {
        var sessions = participantCharacterIds
            .Select(sessionManager.GetByCharacterId)
            .OfType<UserSession>()
            .Where(s => s.ManesSurvival == null && s.ZoneId != (byte)ZoneId.Prison)
            .ToList();
        int count = ManesSurvivalRules.ArenasFor(sessions.Count, Rules.PlayersPerArena);
        if (count == 0)
            return;

        var now = UtcNow;
        int duration = durationSeconds > 0 ? durationSeconds : ManesSurvivalRules.DurationSeconds;
        var opened = Enumerable.Range(0, count)
            .Select(i => new ManesSurvivalArena(ManesSurvivalRules.ArenaZone(i), now, duration))
            .ToList();
        lock (arenasLock)
            arenas.AddRange(opened);

        int capacity = ManesSurvivalRules.Capacity(Rules.PlayersPerArena);
        for (int i = 0; i < sessions.Count && i < capacity; i++)
            await EnterAsync(sessions[i], opened[i % count]);

        foreach (var arena in opened)
        {
            await SpawnBossAsync(arena);
            logger.LogInformation("Manes' Survival arena {Zone} started with {Count} players", arena.Zone, arena.Participants.Count);
        }
    }

    private async Task SpawnBossAsync(ManesSurvivalArena arena)
    {
        var (x, z) = ManesSurvivalRules.BossSpot;
        var invincibleUntil = arena.StartUtc.AddSeconds(ManesSurvivalRules.BossInvincibleSeconds).Ticks;
        var spawned = await npcSummonService.SummonAsync(Rules.BossNpcId, arena.Zone, 0, x, z, 1, 0f, npc =>
        {
            npc.MaxHp = Rules.BossHp;
            npc.Hp = Rules.BossHp;
            npc.InvincibleUntilTicks = invincibleUntil;
        });
        arena.Boss = spawned.FirstOrDefault();
        await SendBossBarAsync(arena);
    }

    private async Task SendBossBarAsync(ManesSurvivalArena arena)
    {
        if (arena.Boss is not { } boss)
            return;
        int hp = arena.BossKilledUtc != null ? 0 : Math.Max(0, boss.Hp);
        arena.LastBossBarHp = hp;
        arena.LastBossBarUtc = UtcNow;
        await BroadcastAsync(arena, ManesSurvivalPacketWriter.Boss(boss.Name, boss.MaxHp, hp, arena.SecondsLeft(UtcNow)));
    }

    private async Task SpawnWaveAsync(ManesSurvivalArena arena)
    {
        var spots = ManesSurvivalRules.MidBossSpots.OrderBy(_ => Random.Shared.Next()).Take(Rules.MidBossesPerWave);
        foreach (var (x, z) in spots)
        {
            var spawned = await npcSummonService.SummonAsync(Rules.MidBossNpcId, arena.Zone, 0, x, z, 1, 0f, npc =>
            {
                npc.MaxHp = Rules.MidBossHp;
                npc.Hp = Rules.MidBossHp;
            });
            lock (arenasLock)
                arena.MidBosses.AddRange(spawned);
        }
        await SendMarkersAsync(arena);
    }

    private async Task SendMarkersAsync(ManesSurvivalArena arena)
    {
        arena.LastMarkersUtc = UtcNow;
        List<(int X, int Z)> points;
        lock (arenasLock)
            points = arena.MidBosses.Where(npc => npc.IsAlive).Select(npc => ((int)npc.X, (int)npc.Z)).ToList();
        if (arena.Boss is { IsAlive: true, IsInvincible: false } boss)
            points.Add(((int)boss.X, (int)boss.Z));
        if (points.Count > 0)
            await BroadcastAsync(arena, ManesSurvivalPacketWriter.Markers(points));
    }

    public void NoteInvincibleHit(UserSession attacker)
    {
        if (attacker.ManesSurvival == null)
            return;
        var now = UtcNow;
        if (invincibleNotices.TryGetValue(attacker.CharacterId, out var last)
            && (now - last).TotalSeconds < ManesSurvivalRules.InvincibleNoticeSeconds)
            return;
        invincibleNotices[attacker.CharacterId] = now;
        pendingInvincibleNotices[attacker.CharacterId] = 0;
    }

    private async Task FlushInvincibleNoticesAsync()
    {
        foreach (var id in pendingInvincibleNotices.Keys.ToList())
        {
            if (pendingInvincibleNotices.TryRemove(id, out _) && sessionManager.GetByCharacterId(id) is { ManesSurvival: not null } attacker)
                await attacker.Client.SendPacket(ManesSurvivalPacketWriter.Invincible());
        }
    }

    private async Task EnterAsync(UserSession session, ManesSurvivalArena arena)
    {
        var row = ManesSurvivalRules.RowFor(session.Nation, session.Class);
        var bag = session.WithLock(TakeBag);
        var entry = new ManesSurvivalEntry(arena, row,
            new ManesSurvivalReturn(session.Level, session.Experience, session.Hp, session.Mp, bag))
        {
            MaxHp = ManesSurvivalRules.MaxHp(Rules, ManesSurvivalRules.StartLevel, 0),
            MaxMp = ManesSurvivalRules.MaxMp(Rules, ManesSurvivalRules.StartLevel),
        };

        var origin = (session.ZoneId, session.X, session.Z);
        session.ManesSurvival = entry;
        session.Level = entry.Level;
        session.Experience = entry.Exp;
        lock (arenasLock)
            arena.Participants.Add(session.CharacterId);

        var (x, z) = gameDataService.GetStartPosition(arena.Zone)?.RandomSpawn(session.Nation) ?? ((short)0, (short)0);
        await zoneTransitionService.ChangeZoneAsync(session, arena.Zone, x, z);
        if (session.ZoneId != arena.Zone)
        {
            await LeaveAsync(session, notify: false);
            return;
        }

        session.InstanceReturn = origin;
        session.RecalculateStatsWithBuffs(gameDataService);
        session.Hp = session.MaxHp;
        session.Mp = session.MaxMp;
        await session.Client.SendPacket(ManesSurvivalPacketWriter.Start(row, arena.SecondsLeft(UtcNow),
            ManesSurvivalRules.ExpToLevel(Rules, entry.Level), entry.Exp, entry.Level));
        await combatNotificationService.SendHpChangeAsync(session);
        await combatNotificationService.SendMspChangeAsync(session);
        await userNotificationService.SendWeightChangeAsync(session);
    }

    private static ItemSlot[] TakeBag(UserSession session)
    {
        var bag = new ItemSlot[InventoryConstants.HaveMax];
        for (int i = 0; i < bag.Length; i++)
        {
            var slot = session.Inventory[InventoryConstants.InventoryStart + i];
            bag[i] = new ItemSlot();
            InventoryJournal.CopyInto(slot, bag[i]);
            slot.Clear();
        }
        return bag;
    }

    private static ExchangeItem Carried(ItemSlot slot, bool countable) => new()
    {
        ItemId = slot.ItemId,
        Durability = slot.Durability,
        Count = slot.Count,
        Flag = slot.Flag,
        ExpiresAt = slot.ExpiresAt,
        UniqueId = slot.UniqueId,
        Countable = countable,
    };

    private static void PutBagBack(UserSession session, ItemSlot[] bag)
    {
        for (int i = 0; i < bag.Length; i++)
            InventoryJournal.CopyInto(bag[i], session.Inventory[InventoryConstants.InventoryStart + i]);
    }

    private async Task<bool> LeaveAsync(UserSession session, bool notify)
    {
        if (session.ManesSurvival is not { } entry)
            return false;

        var lost = session.WithLock(s =>
        {
            var carried = Enumerable.Range(InventoryConstants.InventoryStart, InventoryConstants.HaveMax)
                .Select(i => s.Inventory[i])
                .Where(slot => !slot.IsEmpty && !ManesSurvivalRules.IsEventItem(slot.ItemId))
                .Select(slot => Carried(slot, gameDataService.GetItem(slot.ItemId)?.Countable > 0))
                .ToList();
            PutBagBack(s, entry.Real.Bag);
            return carried.Where(item => !s.ReturnEscrowedItem(item)).ToList();
        });
        foreach (var item in lost)
            logger.LogWarning("Manes' Survival: no room to give {Name} back item {ItemId} x{Count} from the event bag",
                session.Name, item.ItemId, item.Count);
        session.Level = entry.Real.Level;
        session.Experience = entry.Real.Experience;
        session.ManesSurvival = null;
        lock (arenasLock)
            entry.Arena.Participants.Remove(session.CharacterId);

        session.RecalculateStatsWithBuffs(gameDataService);
        session.Hp = session.MaxHp;
        session.Mp = session.MaxMp;
        if (notify)
            await session.Client.SendPacket(EndPacket(session));
        return true;
    }

    private Packet EndPacket(UserSession session)
    {
        var bag = session.WithLock(s => Enumerable.Range(InventoryConstants.InventoryStart, InventoryConstants.HaveMax)
            .Select(i =>
            {
                var copy = new ItemSlot();
                InventoryJournal.CopyInto(s.Inventory[i], copy);
                return copy;
            })
            .ToList());
        var items = bag.Select(slot => new ManesSurvivalPacketWriter.BagItem(slot, petService.ItemInfo(session, slot))).ToList();
        var expToLevel = RebirthBonus.RequiredExperience(gameDataService.GetMaxExpForLevel(session.Level), session.RebirthLevel);
        return ManesSurvivalPacketWriter.End(expToLevel, session.Experience, session.Level, items);
    }

    private async Task SendHomeAsync(UserSession session)
    {
        var home = session.InstanceReturn ?? ((byte)ZoneId.Moradon, 0f, 0f);
        session.InstanceReturn = null;
        await zoneTransitionService.ChangeZoneAsync(session, home.ZoneId, home.X, home.Z);
        await userNotificationService.SendStatUpdateAsync(session);
        await userNotificationService.SendWeightChangeAsync(session);
        await combatNotificationService.SendHpChangeAsync(session);
        await combatNotificationService.SendMspChangeAsync(session);
    }

    private async Task OnPlayerLeavingZoneAsync(UserSession session, byte oldZone)
    {
        if (ManesSurvivalRules.IsZone(oldZone) && await LeaveAsync(session, notify: true))
            session.InstanceReturn = null;
    }

    public Task OnPlayerLogoutAsync(UserSession session) => LeaveAsync(session, notify: false);

    public async Task OnPlayerKilledAsync(UserSession victim, UserSession? killer)
    {
        if (victim.ManesSurvival is not { } lost || lost.Arena.IsCompleted)
            return;
        if (killer?.ManesSurvival is not { } won || !ReferenceEquals(won.Arena, lost.Arena))
            return;

        int points = Rules.KillScore;
        won.Kills++;
        await BroadcastAsync(lost.Arena, ManesSurvivalPacketWriter.Kill(
            (byte)killer.Nation, killer.Name, (byte)victim.Nation, victim.Name, points));
        await GainAsync(killer, won, (long)Rules.ExpPerHunterLevel * lost.Level, points);
    }

    public async Task OnMonsterKilledAsync(NpcInstance npc, UserSession killer)
    {
        var arena = Arenas.FirstOrDefault(a => !a.IsCompleted && a.Zone == npc.ZoneId);
        if (arena == null)
            return;
        var entry = killer.ManesSurvival is { } hunter && ReferenceEquals(hunter.Arena, arena) ? hunter : null;
        long exp = (long)Rules.ExpPerMonsterLevel * npc.Level;

        if (ReferenceEquals(arena.Boss, npc))
        {
            arena.BossKilledUtc = UtcNow;
            arena.BossKillerId = entry != null ? killer.CharacterId : 0;
            await BroadcastAsync(arena, ManesSurvivalPacketWriter.LastBoss(entry != null ? killer.Name : string.Empty));
            await SendBossBarAsync(arena);
            if (entry != null)
                await GainAsync(killer, entry, exp, Rules.BossScore);
            return;
        }

        bool midBoss;
        lock (arenasLock)
            midBoss = arena.MidBosses.Remove(npc);
        if (entry == null)
            return;
        if (midBoss)
        {
            await GrantPassiveAsync(killer, entry);
            await BonusOfferAsync(killer, entry);
            await GainAsync(killer, entry, exp, Rules.MidBossScore);
            return;
        }
        await GainAsync(killer, entry, exp, Rules.MonsterScore);
    }

    private async Task GrantPassiveAsync(UserSession session, ManesSurvivalEntry entry)
    {
        var passives = table.Picks.Where(pick => pick.IsPassive).ToList();
        if (passives.Count == 0)
            return;
        var passive = passives[Random.Shared.Next(passives.Count)];
        entry.Gain(passive);
        entry.MaxHp = ManesSurvivalRules.MaxHp(Rules, entry.Level, entry.HpBonus);
        session.RecalculateStatsWithBuffs(gameDataService);
        session.Hp = (short)Math.Min(session.MaxHp, session.Hp + Math.Max((short)0, passive.Hp));
        await session.Client.SendPacket(ManesSurvivalPacketWriter.Passive(passive.Code));
        await combatNotificationService.SendHpChangeAsync(session);
    }

    private async Task BonusOfferAsync(UserSession session, ManesSurvivalEntry entry)
    {
        var next = table.Picks
            .Where(pick => pick.IsSkill)
            .GroupBy(pick => pick.Group)
            .Select(group => (Owned: entry.GroupLevel(group.Key),
                Pick: group.Where(pick => pick.Level > entry.GroupLevel(group.Key)).MinBy(pick => pick.Level)))
            .Where(choice => choice.Pick != null)
            .OrderByDescending(choice => choice.Owned > 0)
            .ThenBy(_ => Random.Shared.Next())
            .Take(ManesSurvivalRules.OfferSize)
            .Select(choice => choice.Pick!.Code)
            .ToList();
        if (next.Count == 0)
            return;
        entry.Offer(new ManesSurvivalOffer(next, [], Bonus: true));
        await session.Client.SendPacket(ManesSurvivalPacketWriter.BonusOffer(next));
    }

    public async Task BonusPickAsync(UserSession session, ManesPickAction action, short code)
    {
        if (session.ManesSurvival is not { } entry)
            return;
        short result = ManesSurvivalRules.PickFailed;
        if (entry.CurrentOffer is { Bonus: true } offer)
        {
            if (action == ManesPickAction.Skill && offer.Skills.Contains(code)
                && table.Pick(code) is { IsSkill: true } pick && entry.CloseOffer(offer))
            {
                entry.Learn(pick);
                result = ManesSurvivalRules.PickSucceeded;
            }
            else if (action != ManesPickAction.Skill && entry.CloseOffer(offer))
            {
                result = ManesSurvivalRules.PickSucceeded;
            }
        }
        await session.Client.SendPacket(ManesSurvivalPacketWriter.BonusPickReply(result));
    }

    private async Task GainAsync(UserSession session, ManesSurvivalEntry entry, long exp, int points)
    {
        var (level, left, levels) = ManesSurvivalRules.Gain(Rules, entry.Level, entry.Exp, exp);
        entry.Level = level;
        entry.Exp = left;
        entry.Score += points + levels * Rules.LevelScore;
        session.Level = level;
        session.Experience = left;

        if (levels > 0)
        {
            entry.MaxHp = ManesSurvivalRules.MaxHp(Rules, level, entry.HpBonus);
            entry.MaxMp = ManesSurvivalRules.MaxMp(Rules, level);
            session.RecalculateStatsWithBuffs(gameDataService);
            session.Hp = session.MaxHp;
            session.Mp = session.MaxMp;
            await sessionManager.Regions.SendToRegion(session, ManesSurvivalPacketWriter.LevelUp(session.CharacterId, level,
                ManesSurvivalRules.ExpToLevel(Rules, level), left, session.MaxHp, session.Hp, session.MaxMp, session.Mp),
                excludeSender: false);
            for (int i = 0; i < levels; i++)
                await OfferAsync(session, entry);
        }
        else if (exp > 0)
        {
            await session.Client.SendPacket(ManesSurvivalPacketWriter.Exp(left));
        }

        await session.Client.SendPacket(ManesSurvivalPacketWriter.Score(entry.Score));
    }

    private async Task OfferAsync(UserSession session, ManesSurvivalEntry entry)
    {
        var skills = table.Picks
            .Where(pick => pick.IsSkill)
            .GroupBy(pick => pick.Group)
            .Select(group => group.Where(pick => pick.Level > entry.GroupLevel(group.Key)).MinBy(pick => pick.Level))
            .OfType<ManesSurvivalPick>()
            .OrderBy(_ => Random.Shared.Next())
            .Take(ManesSurvivalRules.OfferSize)
            .Select(pick => pick.Code)
            .ToList();
        if (skills.Count == 0)
            return;

        int tier = ManesSurvivalRules.PotionTier(entry.Level);
        var potions = table.Picks
            .Where(pick => pick.IsPotion)
            .GroupBy(pick => pick.Item)
            .Select(kind => kind.OrderBy(pick => pick.Count).ElementAt(Math.Min(tier, kind.Count() - 1)).Code)
            .ToList();

        entry.Offer(new ManesSurvivalOffer(skills, potions, Bonus: false));
        await session.Client.SendPacket(ManesSurvivalPacketWriter.Offer(skills, potions));
    }

    public async Task PickAsync(UserSession session, ManesPickAction action, short code)
    {
        if (session.ManesSurvival is not { } entry)
            return;

        var result = action switch
        {
            ManesPickAction.Skill => Learn(entry, code),
            ManesPickAction.Skip => Skip(entry),
            ManesPickAction.Potion => await BuyAsync(session, entry, code),
            _ => ManesSurvivalRules.PickFailed,
        };
        await session.Client.SendPacket(ManesSurvivalPacketWriter.PickReply(action, result));
    }

    private short Learn(ManesSurvivalEntry entry, short code)
    {
        if (entry.CurrentOffer is not { Bonus: false } offer || !offer.Skills.Contains(code)
            || table.Pick(code) is not { IsSkill: true } pick || !entry.CloseOffer(offer))
            return ManesSurvivalRules.PickFailed;
        entry.Learn(pick);
        return ManesSurvivalRules.PickSucceeded;
    }

    private static short Skip(ManesSurvivalEntry entry) =>
        entry.CurrentOffer is { } offer && entry.CloseOffer(offer) ? ManesSurvivalRules.PickSucceeded : ManesSurvivalRules.PickFailed;

    private async Task<short> BuyAsync(UserSession session, ManesSurvivalEntry entry, short code)
    {
        if (entry.CurrentOffer is not { } offer || !offer.Potions.Contains(code)
            || table.Pick(code) is not { IsPotion: true } potion || gameDataService.GetItem(potion.Item) is not { } item)
            return ManesSurvivalRules.PickFailed;

        var count = (ushort)Math.Max((short)1, potion.Count);
        var bought = session.WithLock(s =>
        {
            if (s.Money < potion.Price)
                return (Result: ManesSurvivalRules.PickNotEnoughNoah, Slot: -1);
            int slot = s.FindSlotForItem(potion.Item, gameDataService, count);
            if (slot < 0)
                return (Result: ManesSurvivalRules.PickBagFull, Slot: -1);
            s.Money -= potion.Price;
            var bag = s.Inventory[slot];
            if (bag.IsEmpty)
                bag.Durability = item.Duration;
            bag.ItemId = potion.Item;
            bag.Count += count;
            return (Result: ManesSurvivalRules.PickSucceeded, Slot: slot);
        });
        if (bought.Result != ManesSurvivalRules.PickSucceeded)
            return bought.Result;

        var placed = session.Inventory[bought.Slot];
        await userNotificationService.SendGoldLossAsync(session, potion.Price);
        await session.Client.SendPacket(ManesSurvivalPacketWriter.BagPut((byte)(bought.Slot - InventoryConstants.InventoryStart),
            placed.ItemId, placed.Count, placed.Durability, placed.Flag));
        await userNotificationService.SendWeightChangeAsync(session);
        return ManesSurvivalRules.PickSucceeded;
    }

    public async Task DiscardAsync(UserSession session, byte bagSlot, int itemId)
    {
        bool removed = session.ManesSurvival != null && bagSlot < InventoryConstants.HaveMax && session.WithLock(s =>
        {
            var slot = s.Inventory[InventoryConstants.InventoryStart + bagSlot];
            if (slot.IsEmpty || slot.ItemId != itemId)
                return false;
            slot.Clear();
            return true;
        });
        if (removed)
            await userNotificationService.SendWeightChangeAsync(session);
        await session.Client.SendPacket(ManesSurvivalPacketWriter.DiscardReply(removed));
    }

    public Packet BuildRanking(UserSession session)
    {
        if (session.ManesSurvival is not { } entry)
            return ManesSurvivalPacketWriter.Rank(ManesSurvivalRules.Unranked, 0, 0, []);

        var members = Members(entry.Arena);
        var ordered = Ordered(members);
        var byId = members.ToDictionary(m => m.CharacterId);
        var rows = ordered.Take(ManesSurvivalRules.RankRows)
            .Select(score => byId[score.CharacterId])
            .Select(member => new ManesSurvivalPacketWriter.RankRow(
                (byte)member.Nation, member.KnightsId, member.KnightsName ?? string.Empty, member.Name,
                member.ManesSurvival?.Kills ?? 0, member.ManesSurvival?.Score ?? 0))
            .ToList();
        return ManesSurvivalPacketWriter.Rank(ManesSurvivalRules.RankOf(ordered, session.CharacterId),
            entry.Kills, entry.Score, rows);
    }

    private static IReadOnlyList<ManesSurvivalRules.Score> Ordered(IEnumerable<UserSession> members) =>
        ManesSurvivalRules.Ordered(members
            .Where(m => m.ManesSurvival != null)
            .Select(m => new ManesSurvivalRules.Score(m.CharacterId, m.ManesSurvival!.Score, m.ManesSurvival.Kills)));

    private List<UserSession> Members(ManesSurvivalArena arena)
    {
        List<int> ids;
        lock (arenasLock)
            ids = arena.Participants.ToList();
        return ids.Select(sessionManager.GetByCharacterId)
            .OfType<UserSession>()
            .Where(s => ReferenceEquals(s.ManesSurvival?.Arena, arena))
            .ToList();
    }

    private async Task BroadcastAsync(ManesSurvivalArena arena, Packet packet)
    {
        foreach (var member in Members(arena))
            await member.Client.SendPacket(packet);
    }

    public async Task TickAsync()
    {
        await FlushInvincibleNoticesAsync();
        var now = UtcNow;
        foreach (var arena in Arenas)
        {
            if (arena.IsCompleted)
                continue;
            if (now >= arena.EndUtc
                || arena.BossKilledUtc is { } killed && (now - killed).TotalSeconds >= Rules.EndAfterBossSeconds)
            {
                await CloseArenaAsync(arena, rewarded: true);
                continue;
            }

            var waves = Rules.MidBossWaveSeconds;
            if (arena.NextWave < waves.Length && now >= arena.StartUtc.AddSeconds(waves[arena.NextWave]))
            {
                arena.NextWave++;
                await SpawnWaveAsync(arena);
            }
            else if ((now - arena.LastMarkersUtc).TotalSeconds >= ManesSurvivalRules.MarkerSeconds)
            {
                await SendMarkersAsync(arena);
            }

            if (arena.Boss is { } boss && arena.BossKilledUtc == null && boss.Hp != arena.LastBossBarHp
                && (now - arena.LastBossBarUtc).TotalSeconds >= ManesSurvivalRules.BossBarSeconds)
                await SendBossBarAsync(arena);
        }
    }

    public async Task FinishAllAsync()
    {
        foreach (var arena in Arenas)
            await CloseArenaAsync(arena, rewarded: true);
    }

    public async Task CancelAllAsync()
    {
        foreach (var arena in Arenas)
            await CloseArenaAsync(arena, rewarded: false);
    }

    private async Task CloseArenaAsync(ManesSurvivalArena arena, bool rewarded)
    {
        lock (arenasLock)
        {
            if (arena.IsCompleted)
                return;
            arena.IsCompleted = true;
        }

        var members = Members(arena);
        var ordered = Ordered(members);
        foreach (var member in members)
        {
            try
            {
                var rank = ManesSurvivalRules.RankOf(ordered, member.CharacterId);
                int score = member.ManesSurvival?.Score ?? 0;
                await LeaveAsync(member, notify: true);
                if (rewarded && !member.IsGM)
                {
                    await RewardAsync(member, rank, score);
                    if (member.CharacterId == arena.BossKillerId)
                        await BossKillerRewardAsync(member, rank, score);
                }
                await SendHomeAsync(member);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send {Name} home from Manes' Survival arena {Zone}", member.Name, arena.Zone);
            }
        }

        List<NpcInstance> bosses;
        lock (arenasLock)
        {
            bosses = [.. arena.MidBosses];
            arena.MidBosses.Clear();
        }
        if (arena.Boss is { } boss)
            bosses.Add(boss);
        foreach (var npc in bosses.Where(npc => npc.IsAlive))
            await npcLifecycleService.DespawnAsync(npc);

        lock (arenasLock)
            arenas.Remove(arena);
        logger.LogInformation("Manes' Survival arena {Zone} closed (rewarded {Rewarded})", arena.Zone, rewarded);
    }

    private async Task BossKillerRewardAsync(UserSession member, byte rank, int score)
    {
        if (Rules.BossKillerItemId <= 0)
            return;
        if (await TempleEventHelpers.TryGiveItemAsync(gameDataService, userNotificationService, member,
                Rules.BossKillerItemId, Rules.BossKillerItemCount, FullBagNotice))
            await member.Client.SendPacket(ManesSurvivalPacketWriter.Reward(rank, score, Rules.BossKillerItemId,
                Rules.BossKillerItemCount, bossKiller: true));
    }

    private async Task RewardAsync(UserSession member, byte rank, int score)
    {
        if (ManesSurvivalRules.RewardFor(Rules.Rewards, rank) is not { } reward)
            return;
        if (await TempleEventHelpers.TryGiveItemAsync(gameDataService, userNotificationService, member,
                reward.ItemId, reward.Count, FullBagNotice))
            await member.Client.SendPacket(ManesSurvivalPacketWriter.Reward(rank, score, reward.ItemId, reward.Count, bossKiller: false));
    }
}
