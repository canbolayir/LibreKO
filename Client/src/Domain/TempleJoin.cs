namespace LibreKO.Domain;

public static class TempleJoin
{
    public const int MapOfChaosCentreText = 18800;
    public const int ChaosIndunType = 24;
    public const int BorderDefenseWarIndunType = 2;
    public const int JuraidMountainIndunType = 100;
    public const int UnderTheCastleIndunType = 101;
    public const int ForgottenTempleIndunType = 102;
    private const int LastBorderDefenseWarIndunType = 7;
    private const int LateBorderDefenseWarIndunType = 10;
    public const string LevelTooHighText = "Your level is too high to apply.";

    public static bool SignedUp(TempleJoinResult result) => result is TempleJoinResult.Joined or TempleJoinResult.AlreadyJoined;

    public static int IndunType(TempleEventType type) => type switch
    {
        TempleEventType.Chaos => ChaosIndunType,
        TempleEventType.BorderDefenseWar => BorderDefenseWarIndunType,
        TempleEventType.JuraidMountain => JuraidMountainIndunType,
        TempleEventType.UnderTheCastle => UnderTheCastleIndunType,
        TempleEventType.ForgottenTemple => ForgottenTempleIndunType,
        _ => 0,
    };

    public static TempleEventType FromIndunType(int indunType) => indunType switch
    {
        ChaosIndunType => TempleEventType.Chaos,
        >= 1 and <= LastBorderDefenseWarIndunType or LateBorderDefenseWarIndunType => TempleEventType.BorderDefenseWar,
        JuraidMountainIndunType => TempleEventType.JuraidMountain,
        UnderTheCastleIndunType => TempleEventType.UnderTheCastle,
        ForgottenTempleIndunType => TempleEventType.ForgottenTemple,
        _ => TempleEventType.None,
    };

    public static int TextId(TempleJoinResult result) => result switch
    {
        TempleJoinResult.AlreadyJoined => 17999,
        TempleJoinResult.WrongPoints => 18002,
        TempleJoinResult.NeedsMapOfChaos => 18801,
        TempleJoinResult.LadderPoints or TempleJoinResult.ApplyLadderPoints => 41005,
        TempleJoinResult.OtherDungeon => 44542,
        TempleJoinResult.LevelTooLow => 44543,
        TempleJoinResult.DuringWar => 44544,
        TempleJoinResult.DuplicateIp => 44422,
        TempleJoinResult.NotOpen => 44545,
        _ => 0,
    };

    public static bool PerNation(TempleEventType type) =>
        type is TempleEventType.BorderDefenseWar or TempleEventType.JuraidMountain;

    public static string CountLine(TempleEventType type, int karus, int elMorad) =>
        PerNation(type) ? $"Karus {karus}  ·  El Morad {elMorad}" : $"Signed up: {karus + elMorad}";

    public static string Clock(int seconds) =>
        $"{seconds % SecondsPerHour / SecondsPerMinute:00}:{seconds % SecondsPerMinute:00}";

    private const int SecondsPerHour = 3600;
    private const int SecondsPerMinute = 60;
}
