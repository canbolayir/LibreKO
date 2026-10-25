using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.Protocol;

public interface IAdminEventScheduleEditor
{
    bool Handles(byte sub);
    Task HandleAsync(UserSession session, byte sub, Packet packet);
    Task SendSchedulesAsync(UserSession session);
}

public sealed class AdminEventScheduleEditor(
    IGameDataService gameData,
    IEventScheduleStore store,
    IEventStartRules startRules,
    IOptions<GameServerSettings> settings,
    ILogger<AdminEventScheduleEditor> logger) : IAdminEventScheduleEditor
{
    public const byte ReqSchedules = 31;
    public const byte ReqRuleSave = 32;
    public const byte ReqRuleDelete = 33;
    public const byte ReqDurationSave = 44;
    public const byte AckSchedules = 0x1B;

    private const byte FirstSiegeWeekday = 1;
    private const int MaxCountdownMinutes = 60;
    private const int RuleBodySize = 1 + 4 + 4 + 1 + 4 + 1 + 1 + 1 + 1 + 1 + 1 + 1;

    private static readonly EventScheduleKind[] HourKinds =
    [
        EventScheduleKind.Krowaz, EventScheduleKind.Bifrost, EventScheduleKind.Bowl,
        EventScheduleKind.NationWar, EventScheduleKind.SnowWar, EventScheduleKind.Wanted,
    ];

    public bool Handles(byte sub) => sub is >= ReqSchedules and <= ReqRuleDelete or ReqDurationSave;

    public Task HandleAsync(UserSession session, byte sub, Packet packet) => sub switch
    {
        ReqSchedules => SendSchedulesAsync(session),
        ReqRuleSave => HandleSaveAsync(session, packet),
        ReqRuleDelete => HandleDeleteAsync(session, packet),
        ReqDurationSave => HandleDurationAsync(session, packet),
        _ => Task.CompletedTask,
    };

    private async Task HandleSaveAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < RuleBodySize)
            return;

        var kind = (EventScheduleKind)packet.ReadByte();
        var owner = packet.ReadInt();
        var ruleId = packet.ReadInt();
        var repeat = (ScheduleRepeat)packet.ReadByte();
        var date = AdminEventPacketWriter.DateFromNumber(packet.ReadInt());
        var weekday = packet.ReadByte();
        DayOfWeek? day = weekday <= (byte)DayOfWeek.Saturday ? (DayOfWeek)weekday : null;
        var dayOfMonth = packet.ReadByte();
        var hour = packet.ReadByte();
        var minute = packet.ReadByte();
        var temple = new TempleRuleFields(packet.ReadByte(), packet.ReadByte(), packet.ReadByte());

        var problem = OwnerProblem(kind, owner)
            ?? (Enum.IsDefined(repeat) ? ScheduleRule.Problem(repeat, date, day, dayOfMonth, hour, minute) : "Unknown repeat.")
            ?? (kind == EventScheduleKind.Temple ? TempleProblem(temple) : null);
        if (problem != null)
        {
            await SendResultAsync(session, false, problem);
            return;
        }

        if (ruleId == 0 && HourKinds.Contains(kind))
            await store.CopyHoursAsync(kind, ConfigHours(kind));

        if (!await store.SaveAsync(kind, owner, ruleId, new RuleFields(repeat, date, day, dayOfMonth, hour, minute), temple))
        {
            await SendResultAsync(session, false, $"No start {ruleId} for {NameOf(kind, owner)}.");
            return;
        }

        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} saved a {Repeat} start of {Event} at {Hour:D2}:{Minute:D2}", session.Name, repeat, NameOf(kind, owner), hour, minute);
        await SendResultAsync(session, true, $"{NameOf(kind, owner)}: start saved.");
        await SendSchedulesAsync(session);
    }

    private async Task HandleDeleteAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 9)
            return;

        var kind = (EventScheduleKind)packet.ReadByte();
        var owner = packet.ReadInt();
        var ruleId = packet.ReadInt();
        if (OwnerProblem(kind, owner) is { } problem)
        {
            await SendResultAsync(session, false, problem);
            return;
        }

        if (HourKinds.Contains(kind) && !gameData.EventStarts.Any(start => start.Kind == (byte)kind))
        {
            await SendResultAsync(session, false, $"{NameOf(kind, owner)} still runs on the server settings; save a start first.");
            return;
        }

        if (!await store.DeleteAsync(kind, owner, ruleId))
        {
            await SendResultAsync(session, false, $"No start {ruleId} for {NameOf(kind, owner)}.");
            return;
        }

        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} removed start {Rule} of {Event}", session.Name, ruleId, NameOf(kind, owner));
        await SendResultAsync(session, true, $"{NameOf(kind, owner)}: start removed.");
        await SendSchedulesAsync(session);
    }

    private async Task HandleDurationAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < sizeof(byte) + sizeof(int) + sizeof(ushort))
            return;

        var kind = (EventScheduleKind)packet.ReadByte();
        packet.ReadInt();
        int minutes = packet.ReadUShort();
        if (!EventStartRules.TimedKinds.Contains(kind))
        {
            await SendResultAsync(session, false, $"{NameOf(kind, 0)} has no length to set here.");
            return;
        }
        if (minutes is < 1 or > EventStartRules.MaxMinutes)
        {
            await SendResultAsync(session, false, $"An event lasts 1 to {EventStartRules.MaxMinutes} minutes.");
            return;
        }

        await store.SaveDurationAsync(kind, minutes);
        await gameData.ReloadEventsAsync();
        logger.LogInformation("GM {Name} set {Event} to last {Minutes} minutes", session.Name, NameOf(kind, 0), minutes);
        await SendResultAsync(session, true, $"{NameOf(kind, 0)} now lasts {minutes} minutes.");
        await SendSchedulesAsync(session);
    }

    private ushort DurationOf(EventScheduleKind kind) => EventStartRules.TimedKinds.Contains(kind)
        ? (ushort)Math.Clamp(EventStartRules.MinutesOf(startRules, kind, EventStartRules.SettingsMinutes(settings.Value, kind)), 0, ushort.MaxValue)
        : (ushort)0;

    private string? OwnerProblem(EventScheduleKind kind, int owner) => kind switch
    {
        EventScheduleKind.Temple => owner > (int)TempleEvent.None && Enum.IsDefined((TempleEvent)owner) ? null : $"No temple event {owner}.",
        EventScheduleKind.CollectionRace => gameData.CollectionRaceTable.ContainsKey(owner) ? null : $"No Collection Race {owner}.",
        EventScheduleKind.Lottery => gameData.LotteryEventTable.ContainsKey(owner) ? null : $"No lottery {owner}.",
        EventScheduleKind.Krowaz or EventScheduleKind.Bifrost or EventScheduleKind.Bowl
            or EventScheduleKind.NationWar or EventScheduleKind.SnowWar or EventScheduleKind.Wanted => null,
        EventScheduleKind.CastleSiege => "The siege war day is set with the castle, not here.",
        _ => "Unknown event.",
    };

    private static string? TempleProblem(TempleRuleFields temple) =>
        temple.MinLevel < 1 || temple.MaxLevel < temple.MinLevel ? "The levels must be 1 or more, the lowest first."
        : temple.CountdownMinutes is < 1 or > MaxCountdownMinutes ? $"The sign-up countdown must be 1 to {MaxCountdownMinutes} minutes."
        : null;

    private int[] ConfigHours(EventScheduleKind kind) => kind switch
    {
        EventScheduleKind.Krowaz => settings.Value.Events.KrowazStartHours,
        EventScheduleKind.Bifrost => settings.Value.Events.BifrostStartHours,
        EventScheduleKind.Bowl => settings.Value.BowlEvent.StartHours,
        _ => [],
    };

    private string NameOf(EventScheduleKind kind, int owner) => kind switch
    {
        EventScheduleKind.Temple => TempleEventRules.NameFor((TempleEvent)owner),
        EventScheduleKind.CollectionRace => gameData.CollectionRaceTable.TryGetValue(owner, out var race) ? race.Name : $"race {owner}",
        EventScheduleKind.Lottery => gameData.LotteryEventTable.TryGetValue(owner, out var draw) ? draw.Name : $"lottery {owner}",
        EventScheduleKind.Krowaz => EventSchedulePacketCoordinator.KrowazName,
        EventScheduleKind.Bifrost => EventSchedulePacketCoordinator.BifrostName,
        EventScheduleKind.Bowl => EventSchedulePacketCoordinator.BowlName,
        EventScheduleKind.CastleSiege => EventSchedulePacketCoordinator.CastleSiegeName,
        EventScheduleKind.NationWar => EventSchedulePacketCoordinator.NationWarName,
        EventScheduleKind.SnowWar => EventSchedulePacketCoordinator.SnowWarName,
        EventScheduleKind.Wanted => EventSchedulePacketCoordinator.WantedName,
        _ => kind.ToString(),
    };

    internal List<AdminEventPacketWriter.Owner> Owners()
    {
        const byte editable = AdminEventPacketWriter.Editable;
        var owners = new List<AdminEventPacketWriter.Owner>();
        foreach (var temple in Enum.GetValues<TempleEvent>().Where(e => e != TempleEvent.None))
            owners.Add(new(EventScheduleKind.Temple, (int)temple, TempleEventRules.NameFor(temple), AdminEventPacketWriter.ScheduleClock,
                (byte)(editable | AdminEventPacketWriter.HasLevels),
                gameData.TempleEventSchedules.Where(s => s.Event == temple)
                    .Select(s => new AdminEventPacketWriter.Rule(s.Id, s, s.MinLevel, s.MaxLevel, s.CountdownMinutes)).ToList()));

        foreach (var kind in HourKinds)
        {
            var rows = gameData.EventStarts.Where(start => start.Kind == (byte)kind).ToList();
            var rules = rows.Count > 0
                ? rows.Select(start => new AdminEventPacketWriter.Rule(start.Id, start, 0, 0, 0)).ToList()
                : startRules.For(kind, ConfigHours(kind)).Select(start => new AdminEventPacketWriter.Rule(0, start, 0, 0, 0)).ToList();
            owners.Add(new(kind, 0, NameOf(kind, 0), AdminEventPacketWriter.ScheduleClock,
                (byte)(editable | (rows.Count > 0 ? 0 : AdminEventPacketWriter.FromSettings)), rules, DurationOf(kind)));
        }

        if (gameData.SiegeWarfare is { } siege && SiegeRules.IsWeekday(siege.WarDay))
            owners.Add(new(EventScheduleKind.CastleSiege, 0, NameOf(EventScheduleKind.CastleSiege, 0), AdminEventPacketWriter.ScheduleClock, 0,
                [new(0, new SiegeStart((DayOfWeek)(siege.WarDay - FirstSiegeWeekday), siege.WarTime, siege.WarMinute), 0, 0, 0)]));

        foreach (var race in gameData.CollectionRaceTable.Values.OrderBy(r => r.Id))
            owners.Add(new(EventScheduleKind.CollectionRace, race.Id, race.Name, AdminEventPacketWriter.UtcClock, editable,
                gameData.CollectionRaceSchedulesByRace[race.Id].Select(s => new AdminEventPacketWriter.Rule(s.Id, s, 0, 0, 0)).ToList()));

        foreach (var draw in gameData.LotteryEventTable.Values.OrderBy(d => d.Id))
            owners.Add(new(EventScheduleKind.Lottery, draw.Id, draw.Name, AdminEventPacketWriter.UtcClock, editable,
                gameData.LotterySchedulesByEvent[draw.Id].Select(s => new AdminEventPacketWriter.Rule(s.Id, s, 0, 0, 0)).ToList()));

        return owners;
    }

    public Task SendSchedulesAsync(UserSession session)
    {
        var events = settings.Value.Events;
        return session.Client.SendPacket(AdminEventPacketWriter.Schedules(AckSchedules, events.ScheduleTimeZone,
            ScheduleTime.OffsetMinutes(events, DateTime.UtcNow), Owners()));
    }

    private static Task SendResultAsync(UserSession session, bool ok, string message) =>
        session.Client.SendPacket(AdminPanelPacketWriter.Result(ok, message));

    private sealed record SiegeStart(DayOfWeek Weekday, byte Hour, byte Minute) : IScheduleRule
    {
        public ScheduleRepeat Repeat => ScheduleRepeat.Weekly;
        public DateOnly? Date => null;
        public DayOfWeek? Day => Weekday;
        public byte DayOfMonth => 0;
    }
}
