namespace LibreKO.Domain;

public readonly record struct DrakiPlateView(int Stage, int SubStage, int SecondsLeft, int ElapsedSeconds, bool Finished, bool GaveUp);

public readonly record struct DrakiPlateLines(string Chapter, string Limit, string Elapsed);

public readonly record struct DrakiFloorTexts(string Floor, string Name);

public static class DrakiTower
{
    public const int Chapters = 5;
    public const int CertificateItem = 810595000;
    public const int OffrankedText = 40089;
    public const string OffrankedFallback = "Offranked";
    public const int UseCertificateText = 40078;
    public const string UseCertificateFallback = "You have used all 3 daily entrance limits. Do you wish to use Certificate of Draki?";
    public const int NoCertificateText = 40079;
    public const string NoCertificateFallback = "You have used all 3 daily entrance limits. You can enter instance by purchsing Certificate of Draki from PUS.";
    public const string ClearLabel = "Clear";
    public const string GiveUpLabel = "Give Up";
    public const int NoCertificate = 0;
    public const int Zone = 95;
    public const int DiedText = 42014;
    public const string DiedFallback = "You have died. You will be sent to town soon.";
    public const int TownQuestionText = 42015;
    public const string TownQuestionFallback = "Do you wish to return to town?";
    private const int ChapterText = 40080;
    private const string ChapterFallback = "Draki's Tower:";
    private const int TimeLimitText = 40081;
    private const string TimeLimitFallback = "Time Limit";
    private const int ElapsedText = 40082;
    private const string ElapsedFallback = "Elapsed Time";
    public const int LastSecondsText = 40083;
    public const string LastSecondsFallback = "20 seconds remaining.";
    public const int PlateLingerSeconds = 10;
    public const double LastSecondsNoticeSeconds = 3.0;
    public const double FloorFadeSeconds = 0.5;
    public const double FloorHoldSeconds = 1.5;
    public const float FloorBandShare = 0.25f;

    private const int MaxRank = 100;
    private const int SecondsPerHour = 3600;
    private const int SecondsPerMinute = 60;

    public static readonly string[] ChapterLines =
    [
        "Sector 1 | The Preparation : Lv.10-29",
        "Sector 2 | The Beginning : Lv.30-49",
        "Sector 3 | The Result : Lv.50-69",
        "Sector 4 | The Failure Ones : Lv.70-83",
        "Sector 5 | The Ultimate Stage : Lv.83 Above",
    ];

    public static bool Ranked(int rank) => rank is >= 1 and <= MaxRank;

    public static bool SectorOpen(int sector, int maxStage) => sector >= 1 && sector <= Math.Min(maxStage, Chapters);

    public static string FinishTime(int seconds)
    {
        int withinHour = seconds % SecondsPerHour;
        return $"{withinHour / SecondsPerMinute:00}:{withinHour % SecondsPerMinute:00}";
    }

    public static string StageEndLabel(bool gaveUp) => gaveUp ? GiveUpLabel : ClearLabel;

    public static string Clock(int seconds) => FinishTime(Math.Max(0, seconds));

    public static DrakiPlateLines PlateLines(DrakiPlateView plate, bool large) => large
        ? new(LabelledLine(ItemData.Text(ChapterText, ChapterFallback), $"{plate.Stage}-{plate.SubStage}"),
              plate.Finished ? StageEndLabel(plate.GaveUp) : LabelledLine(ItemData.Text(TimeLimitText, TimeLimitFallback), Clock(plate.SecondsLeft)),
              LabelledLine(ItemData.Text(ElapsedText, ElapsedFallback), Clock(plate.ElapsedSeconds)))
        : new($"{plate.Stage}-{plate.SubStage}", Clock(plate.SecondsLeft), Clock(plate.ElapsedSeconds));

    private static string LabelledLine(string label, string value) => $"{label} {value}";

    private const int FirstStageNameText = 40084;

    private static readonly string[] StageNameFallbacks =
    [
        "The Preparation",
        "The Beginning",
        "The Result",
        "The Failed Ones",
        "The Ultimate Stage",
    ];

    private static readonly Dictionary<(int Stage, int SubStage), (int Id, string Fallback)> FloorNames = new()
    {
        [(1, 1)] = (42000, "Tower Entrance"),
        [(1, 3)] = (42001, "A Kidnapped Girl"),
        [(1, 6)] = (42002, "Whereabouts of Parents"),
        [(2, 1)] = (42003, "Laboratory"),
        [(2, 3)] = (42004, "A Girl's Parents 1"),
        [(2, 6)] = (42005, "A Girl's Parents 2"),
        [(3, 1)] = (42006, "Bloody War"),
        [(3, 3)] = (42007, "Scorpion King"),
        [(3, 6)] = (42008, "Spellbound Princess"),
        [(4, 1)] = (42009, "Disposed"),
        [(4, 3)] = (42010, "Eternal Loyalty"),
        [(4, 6)] = (42011, "Forgotten Princess"),
        [(5, 1)] = (42012, "Ultimate Subject"),
        [(5, 3)] = (42013, "Draki's Madness"),
    };

    public static DrakiFloorTexts FloorTexts(int stage, int subStage)
    {
        string stageName = stage is >= 1 and <= Chapters
            ? ItemData.Text(FirstStageNameText + stage - 1, StageNameFallbacks[stage - 1])
            : "";
        string name = FloorNames.TryGetValue((stage, subStage), out var text) ? ItemData.Text(text.Id, text.Fallback) : "";
        return new($"{stage}.{stageName} - {subStage}", name);
    }

    public static (int Id, string Fallback)? EnterText(DrakiEnterResult result) => result switch
    {
        DrakiEnterResult.ShowBoard => null,
        DrakiEnterResult.WrongZone => (40066, "Teleporting is disabled in this zone."),
        DrakiEnterResult.AlreadyInInstance => (40067, "You are in an instance dungeon."),
        DrakiEnterResult.FloorNotReached => (40068, "Clear info and entrance info does not match."),
        DrakiEnterResult.NoLimit => (40069, "You have either spent all 3 entrance limits or do not have entrance item."),
        DrakiEnterResult.RoomCreationFailed => (40070, "Instance generation failed"),
        DrakiEnterResult.InvalidItem => (40071, "You do not have the enterance item."),
        DrakiEnterResult.Dead => (40072, "You cannot enter while you are dead."),
        DrakiEnterResult.ServerIndexMismatch => (40073, "Server internal instance index info does not match."),
        DrakiEnterResult.PacketIndexMismatch => (40074, "Packet instance index Info does not match."),
        DrakiEnterResult.NoUserInfo => (40075, "No user information"),
        DrakiEnterResult.CannotEnterNow => (40077, "You cannot enter right now."),
        DrakiEnterResult.SilentRefusal => (40090, ""),
        DrakiEnterResult.LadderPoints => (40091, "You do not have enough ladder point (30 pts) to enter Draki's Tower."),
        DrakiEnterResult.TowerFull => (40076, "Draki's Tower is currently at maximum capacity."),
        _ => (40077, "You cannot enter right now."),
    };
}
