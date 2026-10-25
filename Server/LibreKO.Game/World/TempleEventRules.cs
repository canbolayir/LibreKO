using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class TempleEventRules
{
    public const int SecondsPerMinute = 60;
    public const int JoinWindowSeconds = 600;
    public const byte DefaultCountdownMinutes = 10;
    public const int ChaosDurationSeconds = 1200;
    public const int BorderDefenseWarDurationSeconds = 1800;
    public const int JuraidMountainDurationSeconds = 2700;
    public const int UnderTheCastleDurationSeconds = 3600;
    public const byte JuraidMountainDefaultMinLevel = 70;
    public const byte JuraidMountainDefaultMaxLevel = 83;
    public const byte BorderDefenseWarDefaultMinLevel = 20;
    public const byte BorderDefenseWarDefaultMaxLevel = 83;
    public const byte UnderTheCastleDefaultMinLevel = 70;
    public const byte UnderTheCastleDefaultMaxLevel = 83;
    public const int ForgottenTempleDurationSeconds = 2400;
    public const byte ForgottenTempleLowMinLevel = 46;
    public const byte ForgottenTempleLowMaxLevel = 59;
    public const byte ForgottenTempleHighMinLevel = 60;
    public const byte ForgottenTempleHighMaxLevel = 83;

    public const int ChaosPlayersPerRoom = ChaosRules.PlayersPerRoom;
    public const ushort BorderDefenseWarIndunEvent = 2;
    public const int MapOfChaos = 910246000;
    public const int StartMinuteOfHour = 0;

    public const ushort JuraidMountainIndunEvent = 100;
    public const ushort UnderTheCastleIndunEvent = 101;
    public const ushort ForgottenTempleIndunEvent = 102;
    private const ushort LastBorderDefenseWarIndunEvent = 7;
    private const ushort LateBorderDefenseWarIndunEvent = 10;

    public static ushort IndunType(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => ChaosRules.IndunEvent,
        TempleEvent.BorderDefenseWar => BorderDefenseWarIndunEvent,
        TempleEvent.JuraidMountain => JuraidMountainIndunEvent,
        TempleEvent.UnderTheCastle => UnderTheCastleIndunEvent,
        TempleEvent.ForgottenTemple => ForgottenTempleIndunEvent,
        _ => 0,
    };

    public static TempleEvent ContestOf(ushort indunType) => indunType switch
    {
        ChaosRules.IndunEvent => TempleEvent.Chaos,
        >= 1 and <= LastBorderDefenseWarIndunEvent or LateBorderDefenseWarIndunEvent => TempleEvent.BorderDefenseWar,
        JuraidMountainIndunEvent => TempleEvent.JuraidMountain,
        UnderTheCastleIndunEvent => TempleEvent.UnderTheCastle,
        ForgottenTempleIndunEvent => TempleEvent.ForgottenTemple,
        _ => TempleEvent.None,
    };

    public static bool HasJoinBoard(TempleEvent contest) =>
        contest is TempleEvent.Chaos or TempleEvent.BorderDefenseWar;

    public static bool JoinsFromTheEventBoard(TempleEvent contest) => contest != TempleEvent.ManesSurvival;

    public static byte ZoneFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => (byte)ZoneId.ChaosDungeon,
        TempleEvent.BorderDefenseWar => (byte)ZoneId.BorderDefenseWar,
        TempleEvent.JuraidMountain => (byte)ZoneId.JuradMountain,
        TempleEvent.UnderTheCastle => (byte)ZoneId.UnderCastle,
        TempleEvent.ForgottenTemple => (byte)ZoneId.ForgottenTemple,
        TempleEvent.ManesSurvival => ManesSurvivalRules.FirstZone,
        _ => 0,
    };

    public static int DurationSecondsFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => ChaosDurationSeconds,
        TempleEvent.BorderDefenseWar => BorderDefenseWarDurationSeconds,
        TempleEvent.JuraidMountain => JuraidMountainDurationSeconds,
        TempleEvent.UnderTheCastle => UnderTheCastleDurationSeconds,
        TempleEvent.ForgottenTemple => ForgottenTempleDurationSeconds,
        TempleEvent.ManesSurvival => ManesSurvivalRules.DurationSeconds,
        _ => 0,
    };

    public static bool Fits(byte level, byte minLevel, byte maxLevel) =>
        (minLevel == 0 || level >= minLevel) && (maxLevel == 0 || level <= maxLevel);

    public static byte DefaultMinLevel(TempleEvent contest) => contest switch
    {
        TempleEvent.BorderDefenseWar => BorderDefenseWarDefaultMinLevel,
        TempleEvent.JuraidMountain => JuraidMountainDefaultMinLevel,
        TempleEvent.UnderTheCastle => UnderTheCastleDefaultMinLevel,
        TempleEvent.ForgottenTemple => ForgottenTempleLowMinLevel,
        TempleEvent.ManesSurvival => ManesSurvivalRules.DefaultMinLevel,
        _ => 0,
    };

    public static byte DefaultMaxLevel(TempleEvent contest) => contest switch
    {
        TempleEvent.BorderDefenseWar => BorderDefenseWarDefaultMaxLevel,
        TempleEvent.JuraidMountain => JuraidMountainDefaultMaxLevel,
        TempleEvent.UnderTheCastle => UnderTheCastleDefaultMaxLevel,
        TempleEvent.ForgottenTemple => ForgottenTempleHighMaxLevel,
        _ => 0,
    };

    public static string DescriptionFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => $"Free-for-all arena, {ChaosRules.PlayersPerRoom} players a room",
        TempleEvent.BorderDefenseWar => $"{BorderDefenseWarService.MaxPlayersPerNationPerRoom} vs {BorderDefenseWarService.MaxPlayersPerNationPerRoom} capture the flag",
        TempleEvent.JuraidMountain => $"{JuraidMountainService.MaxPlayersPerNationPerRoom} vs {JuraidMountainService.MaxPlayersPerNationPerRoom} race to slay Devabird",
        TempleEvent.UnderTheCastle => "Boss dungeon, one hour",
        TempleEvent.ForgottenTemple => "Hold out against the monster waves",
        TempleEvent.ManesSurvival => "Free-for-all from level 1 to 30, the boss decides it",
        _ => string.Empty,
    };

    public static string NameFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => "Chaos Dungeon",
        TempleEvent.BorderDefenseWar => "Border Defense War",
        TempleEvent.JuraidMountain => "Juraid Mountain",
        TempleEvent.UnderTheCastle => "Under The Castle",
        TempleEvent.ForgottenTemple => "Forgotten Temple",
        TempleEvent.ManesSurvival => "Manes' Survival",
        _ => "Event",
    };
}
