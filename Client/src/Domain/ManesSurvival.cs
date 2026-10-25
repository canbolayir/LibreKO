using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace LibreKO.Domain;

public sealed record ManesLook(int Row, int Class, int Race, int Right, int Left, int[] Armour);

public sealed record ManesPick(int Code, int Kind, int Magic, int Item, int Count, int Price, int Hp, int AttackUp, int AttackDown);

public sealed record ManesBook(ManesLook[] Looks, ManesPick[] Picks)
{
    public static readonly ManesBook Empty = new([], []);

    public ManesPick? Pick(int code) => Picks.FirstOrDefault(p => p.Code == code);

    public ManesPick? PickOfMagic(int magic) => Picks.FirstOrDefault(p => p.Magic == magic);

    public ManesLook? Look(int row) => Looks.FirstOrDefault(l => l.Row == row);
}

public enum ManesFit
{
    Free,
    Replaces,
    AlreadyHeld,
}

public readonly record struct ManesPassiveTotals(int Hp, int AttackUp, int AttackDown);

public static class ManesSurvival
{
    public const int FirstZone = 57;
    public const int LastZone = 60;

    public const int PassiveKind = 5;
    public const int PotionKind = 100;
    private const int LastSkillKind = 4;
    private const int GroupDivisor = 10;
    private const int PassiveHpScale = 10;

    public const int EventSeconds = 1200;
    public const int NonBattleOffset = 1020;
    public const int InvincibleOffset = 600;
    public const double MarkerSeconds = 10.0;
    public const double OfferBlinkSeconds = 60.0;
    public const double CountdownSeconds = 5.0;
    public const int RankRows = 20;
    public const int BagSlots = 28;

    public const int StrokeSkill = 491201;
    public const int EyesSkill = 491412;
    public const int SprintSkill = 491200;
    public static readonly int[] StartBar = [StrokeSkill, EyesSkill, SprintSkill];
    public const int ElMoradLook = 491400;
    public const int KarusLook = 491401;
    private const int LastKarusRow = 2;
    public const int SightBuffType = 195;
    public const int ManesTree = 3200;

    public const int NotEnoughNoahText = 44404;
    public const int LevelText = 44405;
    public const int FullText = 44406;
    public const int AlreadyText = 44407;
    public const int NoahPriceText = 44408;
    public const int SkipPickText = 44409;
    public const int HunterText = 44410;
    public const int ScoreText = 44411;
    public const int LastBossText = 44412;
    public const int KillText = 44413;
    public const int BossRewardText = 44414;
    public const int RankRewardText = 44415;
    public const int DuplicateIpText = 44422;
    public const int NoBattleText = 44423;
    public const int PickFailedText = 44424;
    public const int WarReadyText = 44428;
    public const int NonBattleText = 44429;
    public const int CancelText = 44430;
    public const int ApplyText = 44431;
    public const int BonusTitleText = 44432;
    public const int PickTitleText = 44433;
    public const int BonusOfferText = 44434;
    public const int DefeatBossText = 44435;
    public const int IncreaseText = 44436;
    public const int DecreaseText = 44437;
    public const int AcquirableText = 44439;
    public const int InvincibleLiftedText = 44446;
    public const int InvincibleTimeText = 44447;

    public static readonly int[] StartTexts = [44416, 44417, 44418, 44419, 44420, 44421, 44425, 44426, 44427, 44445];

    public const string KarusColour = "#f08080";
    public const string ElMoradColour = "#00bfff";
    public const string WinnerColour = "#7dfc32";

    public static bool IsZone(int zone) => zone is >= FirstZone and <= LastZone;

    public static readonly IReadOnlySet<string> RefusedWindows = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "shoppingmall", "events", "seek_party", "mail", "mailcompose", "mailread", "globalmap",
        "merchantmenu", "sellstall", "wantedstall",
    };

    public static bool RefusesWindow(int zone, string windowId) => IsZone(zone) && RefusedWindows.Contains(windowId);

    public static bool HasSight(IReadOnlyDictionary<int, double> buffEnds, double now, Func<int, int> buffTypeOf) =>
        buffEnds.Any(b => b.Value > now && buffTypeOf(b.Key) == SightBuffType);

    public static bool HidesNames(int zone, bool viewerIsGm, bool sight) => IsZone(zone) && !viewerIsGm && !sight;

    public static bool IsSkill(int kind) => kind is >= 1 and <= LastSkillKind;

    public static int LookSkill(int row) => row <= LastKarusRow ? KarusLook : ElMoradLook;

    public static ManesFit Fit(int heldCode, int offeredCode) =>
        heldCode / GroupDivisor != offeredCode / GroupDivisor ? ManesFit.Free
        : heldCode >= offeredCode ? ManesFit.AlreadyHeld
        : ManesFit.Replaces;

    public static int NonBattleLeft(int seconds) => Math.Max(0, seconds - NonBattleOffset);

    public static int InvincibleLeft(int seconds) => Math.Max(0, seconds - InvincibleOffset);

    public static string Clock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public static int SignUpText(int result) => result switch
    {
        -4 => DuplicateIpText,
        -3 => AlreadyText,
        -2 => FullText,
        -1 => LevelText,
        _ => 0,
    };

    public static string PickFailure(int result, Func<int, string, string> text) => result switch
    {
        -11 => text(NotEnoughNoahText, "You do not have enough noah."),
        < 0 => $"{text(PickFailedText, "Failed to acquire skill.")} ({-result})",
        _ => "",
    };

    public static ManesPassiveTotals Totals(IEnumerable<ManesPick> passives)
    {
        int hp = 0, up = 0, down = 0;
        foreach (var p in passives)
        {
            hp += p.Hp;
            up += p.AttackUp;
            down += p.AttackDown;
        }
        return new ManesPassiveTotals(hp * PassiveHpScale, up, down);
    }

    public static string NationColour(int nation) => nation == Nations.Karus ? KarusColour : ElMoradColour;

    public static ManesBook Parse(string json) =>
        JsonSerializer.Deserialize(json, DomainJson.Default.ManesBook) ?? ManesBook.Empty;
}
