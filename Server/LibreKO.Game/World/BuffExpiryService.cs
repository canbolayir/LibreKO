using LibreKO.Game.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.World;

public class BuffExpiryService(
    SessionManager sessionManager,
    IMagicExecutionService magicExecutionService,
    ICombatLifecycleService combatLifecycleService,
    ICombatNotificationService combatNotificationService,
    IControlStateService controlStateService,
    IRespawnBlinkService respawnBlinkService,
    ILogger<BuffExpiryService> logger,
    IPartyWarBoardService? partyWarBoard = null,
    INpcAiDeathService? npcAiDeathService = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Buff expiry service started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessTickAsync(DateTime.UtcNow.Ticks);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in buff expiry tick");
            }
        }
    }

    public async Task ProcessTickAsync(long nowTicks)
    {
        foreach (var session in sessionManager.GetAll())
        {
            if (session.IsBot)
                continue;

            await ProcessOverTimeEffectsAsync(session, nowTicks);
            await respawnBlinkService.ExpireAsync(session, nowTicks);

            if (session.ActiveBuffs.Count > 0)
            {
                var expired = session.ActiveBuffs
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var skillId in expired)
                    await magicExecutionService.CancelAsync(session, skillId);
            }

            if (session.ActiveBuffs.Count > 0 || session.ControlSignature != 0)
                await controlStateService.SyncAsync(session);
        }

        foreach (var npc in sessionManager.Regions.GetAllNpcs())
        {
            await ProcessOverTimeEffectsAsync(npc, nowTicks);
            if (!npc.Debuffs.IsEmpty || npc.ControlSignature != 0)
                await controlStateService.SyncAsync(npc);
        }
    }

    private async Task ProcessOverTimeEffectsAsync(UserSession session, long nowTicks)
    {
        if (session.ActiveOverTimeEffects.Count == 0)
            return;

        if (session.Hp <= 0)
        {
            session.ActiveOverTimeEffects.Clear();
            return;
        }

        var expiredSkillIds = new List<int>();
        foreach (var (skillId, effect) in session.ActiveOverTimeEffects.ToList())
        {
            while (effect.TickCount < effect.TickLimit && effect.NextTickTicks <= nowTicks)
            {
                if (effect.TickAmount < 0)
                {
                    var killer = sessionManager.GetByCharacterId(effect.CasterId);
                    var npcSource = effect.CasterId < 0 ? sessionManager.Regions.GetNpc(-effect.CasterId) : null;
                    var damage = killer != null
                        ? GmMode.Dealt(killer, session.Hp, -effect.TickAmount)
                        : -effect.TickAmount;
                    session.Hp = (short)Math.Max(0, session.Hp - GmMode.Taken(session, damage));
                    await combatLifecycleService.SendHpChangeAsync(
                        session,
                        killer?.CharacterId ?? npcSource?.UniqueId ?? -1);
                    if (killer != null && killer != session)
                        partyWarBoard?.CountDamage(killer, damage);

                    if (session.Hp <= 0)
                    {
                        expiredSkillIds.Add(skillId);
                        if (npcSource != null && npcAiDeathService != null)
                            await npcAiDeathService.HandlePlayerKilledByNpcAsync(session, npcSource);
                        else
                            await combatLifecycleService.HandlePlayerDeathAsync(session, killer);
                        break;
                    }
                }
                else if (effect.TickAmount > 0)
                {
                    var before = session.Hp;
                    session.Hp = (short)Math.Min(session.MaxHp, session.Hp + effect.TickAmount);
                    await combatLifecycleService.SendHpChangeAsync(session);
                    if (effect.CasterId != session.CharacterId && sessionManager.GetByCharacterId(effect.CasterId) is { } healer)
                        partyWarBoard?.CountHealing(healer, session.Hp - before);
                }

                effect.TickCount++;
                effect.NextTickTicks += TimeSpan.FromSeconds(effect.TickIntervalSeconds).Ticks;
            }

            if (effect.TickCount >= effect.TickLimit)
                expiredSkillIds.Add(skillId);
        }

        // Track which icon flavors were active before removal so we can clear each
        // one ONLY if no other active DOT of the same flavor remains on the target.
        var clearedCodes = new HashSet<byte>();
        foreach (var skillId in expiredSkillIds)
        {
            if (session.ActiveOverTimeEffects.TryRemove(skillId, out var removed)
                && removed.TickAmount < 0
                && removed.PartyStatusCode > 0)
            {
                clearedCodes.Add(removed.PartyStatusCode);
            }
        }
        if (clearedCodes.Count == 0) return;

        // For each flavor that just lost an effect, only clear the panel icon if
        // no remaining DOT carries the same code. Otherwise the player still has
        // (e.g.) poison from a different stack and the icon must stay lit.
        foreach (var code in clearedCodes)
        {
            var stillActive = session.ActiveOverTimeEffects.Any(kv =>
                kv.Value.TickAmount < 0 && kv.Value.PartyStatusCode == code);
            if (!stillActive)
                await combatNotificationService.SendPartyStatusUpdateAsync(session, code, applied: false);
        }
    }

    private async Task ProcessOverTimeEffectsAsync(NpcInstance npc, long nowTicks)
    {
        if (npc.ActiveOverTimeEffects.Count == 0)
            return;

        if (!npc.IsAlive)
        {
            npc.ActiveOverTimeEffects.Clear();
            return;
        }

        var expiredSkillIds = new List<int>();
        foreach (var (skillId, effect) in npc.ActiveOverTimeEffects.ToList())
        {
            while (effect.TickCount < effect.TickLimit && effect.NextTickTicks <= nowTicks)
            {
                if (effect.TickAmount < 0)
                {
                    var killer = sessionManager.GetByCharacterId(effect.CasterId)
                        ?? (npc.TopDamagerCharId > 0 ? sessionManager.GetByCharacterId(npc.TopDamagerCharId) : null);
                    var damage = npc.DamageTaken(killer != null
                        ? GmMode.Dealt(killer, npc.Hp, -effect.TickAmount)
                        : -effect.TickAmount, basicHit: false);
                    npc.Hp = Math.Max(0, npc.Hp - damage);

                    if (killer != null)
                    {
                        npc.RecordDamage(effect.CasterId, damage, killer, id => sessionManager.GetByCharacterId(id));
                        await combatLifecycleService.SendNpcTargetHpAsync(killer, npc, damage);
                    }
                    else
                    {
                        npc.RecordDamage(effect.CasterId, damage);
                    }

                    if (npc.Hp <= 0)
                    {
                        expiredSkillIds.Add(skillId);
                        if (killer != null)
                            await combatLifecycleService.HandleNpcDeathAsync(npc, killer);
                        break;
                    }
                }
                else if (effect.TickAmount > 0)
                {
                    npc.Hp = Math.Min(npc.MaxHp, npc.Hp + effect.TickAmount);
                }

                effect.TickCount++;
                effect.NextTickTicks += TimeSpan.FromSeconds(effect.TickIntervalSeconds).Ticks;
            }

            if (effect.TickCount >= effect.TickLimit)
                expiredSkillIds.Add(skillId);
        }

        foreach (var skillId in expiredSkillIds)
            npc.ActiveOverTimeEffects.TryRemove(skillId, out _);
    }
}
