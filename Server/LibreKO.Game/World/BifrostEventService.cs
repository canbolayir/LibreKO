using LibreKO.Common.Enums;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Common.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public interface IBifrostEventService
{
    long RemainingSecs { get; }
    bool IsActive { get; }
    BifrostPhase Phase { get; }
    AccountNation WinnerNation { get; }
    long LoserDelayRemainingSecs { get; }

    void Start(int? monumentMinutes = null, int? loserDelayMinutes = null, int? farmingMinutes = null);
    Task CloseAsync();
    void Close();
    void SetWinner(AccountNation nation, string killerName = "GM");
    Task OnMonumentKilledAsync(NpcInstance npc, UserSession killer);
    BifrostEntryResult CanEnter(UserSession session);
    Task<bool> TryEnterAsync(UserSession session);
    Task TickAsync();
}

public class BifrostEventService : BackgroundService, IBifrostEventService
{
    private readonly SessionManager sessionManager;
    private readonly IOptions<GameServerSettings> settings;
    private readonly ScheduleClock scheduleClock;
    private readonly IEventStartRules? startRules;
    private readonly IZoneTransitionService zoneTransitionService;
    private readonly ILogger<BifrostEventService> logger;

    private readonly object stateLock = new();
    private long remainingSecs;
    private long loserDelayRemainingSecs;
    private bool isActive;
    private DateTime lastStartMinute = DateTime.MinValue;
    private BifrostPhase phase = BifrostPhase.Inactive;
    private AccountNation winnerNation = AccountNation.None;

    private int monumentDurationMinutes = BifrostRules.DefaultMonumentMinutes;
    private int loserDelayMinutes = BifrostRules.DefaultLoserDelayMinutes;
    private int farmingDurationMinutes = BifrostRules.DefaultFarmingMinutes;

    public BifrostEventService(
        SessionManager sessionManager,
        IOptions<GameServerSettings> settings,
        ScheduleClock scheduleClock,
        IZoneTransitionService zoneTransitionService,
        ILogger<BifrostEventService> logger,
        IEventStartRules? startRules = null)
    {
        this.startRules = startRules;
        this.sessionManager = sessionManager;
        this.settings = settings;
        this.scheduleClock = scheduleClock;
        this.zoneTransitionService = zoneTransitionService;
        this.logger = logger;

        this.zoneTransitionService.ZoneAbilitySent += OnZoneAbilitySentAsync;
    }

    public long RemainingSecs
    {
        get { lock (stateLock) return remainingSecs; }
    }

    public long LoserDelayRemainingSecs
    {
        get { lock (stateLock) return loserDelayRemainingSecs; }
    }

    public bool IsActive
    {
        get { lock (stateLock) return isActive; }
    }

    public BifrostPhase Phase
    {
        get { lock (stateLock) return phase; }
    }

    public AccountNation WinnerNation
    {
        get { lock (stateLock) return winnerNation; }
    }

    public void Start(int? monumentMinutes = null, int? loserDelayMinutes = null, int? farmingMinutes = null)
    {
        lock (stateLock)
        {
            if (isActive)
            {
                logger.LogWarning("Bifrost start requested but event already active; ignored");
                return;
            }

            monumentDurationMinutes = monumentMinutes ?? BifrostRules.DefaultMonumentMinutes;
            this.loserDelayMinutes = loserDelayMinutes ?? BifrostRules.DefaultLoserDelayMinutes;
            farmingDurationMinutes = farmingMinutes ?? BifrostRules.DefaultFarmingMinutes;

            remainingSecs = (long)monumentDurationMinutes * 60;
            loserDelayRemainingSecs = 0;
            winnerNation = AccountNation.None;
            phase = BifrostPhase.MonumentFight;
            isActive = true;
            logger.LogInformation("Bifrost event started (Monument phase): {Minutes}min ({Secs}s)", monumentDurationMinutes, remainingSecs);
        }

        var monument = sessionManager.Regions.GetNpcByProtoId(BifrostRules.ZoneRonarkLand, BifrostRules.MonumentNpcId);
        if (monument != null)
        {
            monument.IsMonumentAttackable = true;
            if (monument.IsDead)
            {
                monument.Respawn();
                sessionManager.Regions.UpdateNpcRegion(monument);
                _ = sessionManager.Regions.BroadcastFromNpc(monument, NpcPacketMapper.BuildInOutPacket(monument, InOutType.In));
            }
        }

        _ = BroadcastNoticeAsync(BifrostRules.NoticeStart);
        _ = BroadcastRemainingAsync();
    }

    public void Close()
    {
        _ = CloseAsync();
    }

    public async Task CloseAsync()
    {
        bool wasActive;
        lock (stateLock)
        {
            wasActive = isActive;
            isActive = false;
            phase = BifrostPhase.Inactive;
            remainingSecs = 0;
            loserDelayRemainingSecs = 0;
            winnerNation = AccountNation.None;
        }

        if (wasActive)
        {
            logger.LogInformation("Bifrost event closed");
            var monument = sessionManager.Regions.GetNpcByProtoId(BifrostRules.ZoneRonarkLand, BifrostRules.MonumentNpcId);
            if (monument != null)
                monument.IsMonumentAttackable = false;

            await BroadcastNoticeAsync(BifrostRules.NoticeFinish);
            await BroadcastRemainingAsync();
            await SweepPlayersAsync();
        }
    }

    public void SetWinner(AccountNation nation, string killerName = "GM")
    {
        lock (stateLock)
        {
            isActive = true;
            phase = BifrostPhase.Farming;
            winnerNation = nation;
            remainingSecs = (long)farmingDurationMinutes * 60;
            loserDelayRemainingSecs = (long)loserDelayMinutes * 60;
            logger.LogInformation("Bifrost forced winner: {Nation} by {Killer}", nation, killerName);
        }

        var monument = sessionManager.Regions.GetNpcByProtoId(BifrostRules.ZoneRonarkLand, BifrostRules.MonumentNpcId);
        if (monument != null)
            monument.IsMonumentAttackable = false;

        _ = BroadcastNoticeAsync(BifrostRules.NoticeVictory(nation));
        _ = BroadcastRemainingAsync();
    }

    public async Task OnMonumentKilledAsync(NpcInstance npc, UserSession killer)
    {
        if (npc.ZoneId != BifrostRules.ZoneRonarkLand || npc.NpcId != BifrostRules.MonumentNpcId)
            return;

        npc.IsMonumentAttackable = false;

        bool shouldTransition = false;
        AccountNation nation = killer.Nation;
        string killerName = killer.Name;

        lock (stateLock)
        {
            if (!isActive || phase != BifrostPhase.MonumentFight)
                return;

            phase = BifrostPhase.Farming;
            winnerNation = nation;
            remainingSecs = (long)farmingDurationMinutes * 60;
            loserDelayRemainingSecs = (long)loserDelayMinutes * 60;
            shouldTransition = true;
        }

        if (shouldTransition)
        {
            logger.LogInformation("Bifrost Monument destroyed by {Killer} ({Nation}). Farming phase started.", killerName, nation);
            await BroadcastNoticeAsync(BifrostRules.NoticeVictory(nation));
            await BroadcastRemainingAsync();
        }
    }

    public BifrostEntryResult CanEnter(UserSession session)
    {
        lock (stateLock)
        {
            if (!isActive)
                return BifrostEntryResult.NotActive;

            if (phase == BifrostPhase.MonumentFight)
                return BifrostEntryResult.MonumentNotFallen;

            if (phase != BifrostPhase.Farming)
                return BifrostEntryResult.NotActive;

            if (session.Nation == winnerNation)
                return BifrostEntryResult.Success;

            if (loserDelayRemainingSecs > 0)
                return BifrostEntryResult.LoserDelayActive;

            return BifrostEntryResult.Success;
        }
    }

    public async Task<bool> TryEnterAsync(UserSession session)
    {
        if (session.Hp <= 0)
            return false;

        var check = CanEnter(session);
        switch (check)
        {
            case BifrostEntryResult.NotActive:
                await session.Client.SendPacket(ChatPacketWriter.SystemNotice((byte)session.Nation, BifrostRules.NoticeEventNotActive));
                return false;

            case BifrostEntryResult.MonumentNotFallen:
                await session.Client.SendPacket(ChatPacketWriter.SystemNotice((byte)session.Nation, BifrostRules.NoticeMonumentNotFallen));
                return false;

            case BifrostEntryResult.LoserDelayActive:
                var delayMinutes = (int)Math.Ceiling(LoserDelayRemainingSecs / 60.0);
                var notice = $"{BifrostRules.NoticeCannotEnterYet} ({delayMinutes} min remaining)";
                await session.Client.SendPacket(ChatPacketWriter.SystemNotice((byte)session.Nation, notice));
                return false;

            case BifrostEntryResult.Success:
                float targetX = session.Nation == AccountNation.Karus ? BifrostRules.KarusEntryX : BifrostRules.ElMoradEntryX;
                float targetZ = session.Nation == AccountNation.Karus ? BifrostRules.KarusEntryZ : BifrostRules.ElMoradEntryZ;
                await zoneTransitionService.ChangeZoneAsync(session, BifrostRules.ZoneBifrost, targetX, targetZ);
                return true;

            default:
                return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                TickSchedule(ScheduleTime.Now(settings.Value.Events, scheduleClock.UtcNow));
                await TickAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Bifrost tick error");
            }
        }
    }

    internal void TickSchedule(DateTime now)
    {
        var minute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute);
        if (IsActive || minute == lastStartMinute
            || !EventStartRules.StartsAt(startRules, EventScheduleKind.Bifrost, settings.Value.Events.BifrostStartHours, now))
            return;
        lastStartMinute = minute;
        Start();
    }

    public async Task TickAsync()
    {
        bool monumentTimedOut = false;
        bool loserDelayJustEnded = false;
        bool farmingTimedOut = false;

        lock (stateLock)
        {
            if (!isActive)
                return;

            if (phase == BifrostPhase.MonumentFight)
            {
                if (remainingSecs > 0)
                {
                    remainingSecs--;
                    if (remainingSecs == 0)
                    {
                        isActive = false;
                        phase = BifrostPhase.Inactive;
                        monumentTimedOut = true;
                    }
                }
            }
            else if (phase == BifrostPhase.Farming)
            {
                if (loserDelayRemainingSecs > 0)
                {
                    loserDelayRemainingSecs--;
                    if (loserDelayRemainingSecs == 0)
                    {
                        loserDelayJustEnded = true;
                    }
                }

                if (remainingSecs > 0)
                {
                    remainingSecs--;
                    if (remainingSecs == 0)
                    {
                        isActive = false;
                        phase = BifrostPhase.Inactive;
                        farmingTimedOut = true;
                    }
                }
            }
        }

        if (monumentTimedOut)
        {
            logger.LogInformation("Bifrost event ended without monument kill (monument phase elapsed)");
            var monument = sessionManager.Regions.GetNpcByProtoId(BifrostRules.ZoneRonarkLand, BifrostRules.MonumentNpcId);
            if (monument != null)
                monument.IsMonumentAttackable = false;

            await BroadcastNoticeAsync(BifrostRules.NoticeFinish);
            await BroadcastRemainingAsync();
        }
        else if (loserDelayJustEnded)
        {
            logger.LogInformation("Bifrost gates are now open to all nations");
            await BroadcastNoticeAsync(BifrostRules.NoticeGatesOpenAll);
        }
        else if (farmingTimedOut)
        {
            logger.LogInformation("Bifrost farming phase expired. Sweeping players.");
            await BroadcastNoticeAsync(BifrostRules.NoticeFinish);
            await BroadcastRemainingAsync();
            await SweepPlayersAsync();
        }
    }

    private async Task SweepPlayersAsync()
    {
        foreach (var session in sessionManager.GetAll())
        {
            if (session.ZoneId == BifrostRules.ZoneBifrost)
            {
                try
                {
                    await zoneTransitionService.ChangeZoneAsync(
                        session,
                        BifrostRules.ZoneRonarkLand,
                        0f,
                        0f);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to sweep player {Player} from Bifrost zone", session.Name);
                }
            }
        }
    }

    private async Task BroadcastNoticeAsync(string text)
    {
        var pkt = NoticePacketWriter.Broadcast(text);
        foreach (var session in sessionManager.GetAll())
        {
            try
            {
                await session.Client.SendPacket(pkt);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private Task BroadcastRemainingAsync()
    {
        var remaining = (int)Math.Min(RemainingSecs, int.MaxValue);
        var pkt = BifrostPacketWriter.Remaining(
            TempleSubOpcode.BifrostRemaining, remaining, eventType: 0);

        foreach (var session in sessionManager.GetAll())
        {
            if (session.ZoneId != BifrostRules.ZoneBifrost && session.ZoneId != BifrostRules.ZoneRonarkLand)
                continue;
            try
            {
                _ = session.Client.SendPacket(pkt);
            }
            catch
            {
                // best-effort broadcast
            }
        }
        return Task.CompletedTask;
    }

    private async Task OnZoneAbilitySentAsync(UserSession session)
    {
        if (session.ZoneId != BifrostRules.ZoneBifrost && session.ZoneId != BifrostRules.ZoneRonarkLand)
            return;

        if (!IsActive)
            return;

        var remaining = (int)Math.Min(RemainingSecs, int.MaxValue);
        var pkt = BifrostPacketWriter.Remaining(
            TempleSubOpcode.BifrostRemaining, remaining, eventType: 0);
        try
        {
            await session.Client.SendPacket(pkt);
        }
        catch
        {
            // best-effort
        }
    }
}
