using System.Collections.Generic;

namespace LibreKO.Domain;

public static class CombatLogLines
{
    public const int HpDamageText = 3009;
    public const int HpRecoveredText = 3010;
    public const int MpRecoveredText = 3011;
    public const int MpUsedText = 3012;
    public const int SpRecoveredText = 3013;
    public const int SpUsedText = 3014;
    public const int PlayerDamageText = 4557;
    public const int KilledByText = 3038;
    public const int TargetDamageText = 3016;
    public const int TenfoldScale = 10;
    private const int MageFamily = 3;
    private const int PriestFamily = 4;

    private static readonly HashSet<int> TenfoldZones = new() { 45, 57, 58, 59, 60, 85, 89 };

    public static int HpTextScale(int zone) => TenfoldZones.Contains(zone) ? TenfoldScale : 1;

    public static int LoggedHp(int zone, int amount) => amount * HpTextScale(zone);

    public static bool UsesMp(int classCode) => CharacterClassCatalog.Family(classCode) is MageFamily or PriestFamily;

    public static int ManaText(int classCode, bool recovered) => UsesMp(classCode)
        ? recovered ? MpRecoveredText : MpUsedText
        : recovered ? SpRecoveredText : SpUsedText;
}
