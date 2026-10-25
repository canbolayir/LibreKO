using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public readonly record struct GmCommandInfo(string Group, string Syntax, string Description);

public static class GmCommands
{
    public const string Goto = "+goto";
    public const string SummonUser = "+summonuser";

    public static string ForPlayer(string command, string name) => $"{command} {name}";

    public static string? StartEvent(EventOwner owner) => owner.Kind switch
    {
        EventKind.Temple => (TempleEventType)owner.Id switch
        {
            TempleEventType.Chaos => "+chaos",
            TempleEventType.BorderDefenseWar => "+bdw",
            TempleEventType.JuraidMountain => "+jr",
            TempleEventType.UnderTheCastle => "+utc open",
            TempleEventType.ForgottenTemple => "+ft",
            TempleEventType.ManesSurvival => "+manes",
            _ => null,
        },
        EventKind.Krowaz => "+krowaz open",
        EventKind.Bifrost => "+bifrost start",
        EventKind.Bowl => "+bowl on",
        EventKind.CastleSiege => "+siegewar countdown",
        EventKind.CollectionRace => $"+cropen {owner.Id}",
        EventKind.Lottery => $"+lottery start {owner.Id}",
        EventKind.SnowWar => "+snowwar",
        EventKind.Wanted => "+wanted on",
        _ => null,
    };

    public static string? StopEvent(EventOwner owner) => owner.Kind switch
    {
        EventKind.Temple => (TempleEventType)owner.Id switch
        {
            TempleEventType.UnderTheCastle => "+utc close",
            TempleEventType.ForgottenTemple => "+ft close",
            TempleEventType.ManesSurvival => "+manes cancel",
            _ => "+templecancel",
        },
        EventKind.Krowaz => "+krowaz close",
        EventKind.Bifrost => "+bifrost close",
        EventKind.Bowl => "+bowl off",
        EventKind.CastleSiege => "+siegewar off",
        EventKind.CollectionRace => "+crclose",
        EventKind.Lottery => "+lottery close",
        EventKind.NationWar or EventKind.SnowWar => "+warclose",
        EventKind.Wanted => "+wanted off",
        _ => null,
    };

    public static bool Matches(GmCommandInfo command, string search)
    {
        string needle = search.Trim();
        return needle.Length == 0
            || command.Syntax.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || command.Description.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || command.Group.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<(string Group, IReadOnlyList<GmCommandInfo> Commands)> Grouped(
        IEnumerable<GmCommandInfo> commands, string search) =>
        commands.Where(command => Matches(command, search))
            .GroupBy(command => command.Group)
            .Select(group => (group.Key, (IReadOnlyList<GmCommandInfo>)group.ToList()))
            .ToList();
}
