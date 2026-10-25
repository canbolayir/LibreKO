using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public sealed class CommandEventScheduler(
    SessionManager sessionManager,
    IEventSystemsPacketCoordinator eventSystems,
    IVanguardService vanguard,
    IEventStartRules startRules,
    ScheduleClock scheduleClock,
    IOptions<GameServerSettings> settings,
    ILogger<CommandEventScheduler> logger) : BackgroundService
{
    private static readonly byte[] WarZones =
    [
        BattleZoneManager.ZONE_BATTLE1, BattleZoneManager.ZONE_BATTLE2, BattleZoneManager.ZONE_BATTLE3,
        BattleZoneManager.ZONE_BATTLE4, BattleZoneManager.ZONE_BATTLE5, BattleZoneManager.ZONE_BATTLE6,
    ];

    private DateTime lastMinute = DateTime.MinValue;
    private DateTime? wantedUntil;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(DateTime.UtcNow, ScheduleTime.Now(settings.Value.Events, scheduleClock.UtcNow));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in the scheduled command events tick");
            }
        }
    }

    internal async Task TickAsync(DateTime utcNow, DateTime scheduleNow)
    {
        if (wantedUntil is { } until && utcNow >= until)
        {
            wantedUntil = null;
            if (vanguard.Enabled)
                await vanguard.StopAsync();
        }

        var minute = scheduleNow.Date.AddHours(scheduleNow.Hour).AddMinutes(scheduleNow.Minute);
        if (minute == lastMinute)
            return;
        lastMinute = minute;

        if (!sessionManager.Battle.IsBattleActive && Starts(EventScheduleKind.NationWar, scheduleNow))
        {
            var zone = WarZones[Random.Shared.Next(WarZones.Length)];
            logger.LogInformation("Scheduled national war opens in zone {Zone}", zone);
            await eventSystems.OpenBattleZoneAsync(BattleZoneManager.BATTLEZONE_OPEN, zone);
        }
        else if (!sessionManager.Battle.IsBattleActive && Starts(EventScheduleKind.SnowWar, scheduleNow))
        {
            logger.LogInformation("Scheduled snow war opens");
            await eventSystems.OpenBattleZoneAsync(BattleZoneManager.SNOW_BATTLEZONE_OPEN, BattleZoneManager.ZONE_SNOW_BATTLE);
        }

        if (!vanguard.Enabled && Starts(EventScheduleKind.Wanted, scheduleNow))
        {
            var minutes = EventStartRules.MinutesOf(startRules, EventScheduleKind.Wanted, settings.Value.Events.WantedMinutes);
            logger.LogInformation("Scheduled Wanted event starts for {Minutes} minutes", minutes);
            await vanguard.StartAsync();
            wantedUntil = utcNow.AddMinutes(minutes);
        }
    }

    private bool Starts(EventScheduleKind kind, DateTime scheduleNow) =>
        startRules.For(kind, []).Any(rule => rule.Matches(scheduleNow));
}
