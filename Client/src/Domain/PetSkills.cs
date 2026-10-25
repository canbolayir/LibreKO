using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public static class PetSkills
{
    public const int DesignatedAttack = 301_000;
    public const int FirstSkill = 301_000;
    public const int LastSkill = 400_000;
    public const int SharedPageFirst = 1000;
    public const int SharedPageLast = 1009;
    public const int ClassPageDivisor = 10;
    public const int OwnerMoral = 33;
    public const int SlotsPerPage = 8;
    public const int Pages = 2;
    public const int StageCasting = 1;
    public const int StageEffecting = 3;

    public static bool BelongsTo(int skillId, int page, int petClass) =>
        skillId is >= FirstSkill and <= LastSkill
        && (page is >= SharedPageFirst and <= SharedPageLast || page / ClassPageDivisor == petClass);

    public static List<int> Owned(IEnumerable<(int Id, int Page)> skills, int petClass) =>
        skills.Where(s => s.Id != DesignatedAttack && BelongsTo(s.Id, s.Page, petClass))
              .Select(s => s.Id)
              .OrderBy(id => id)
              .ToList();

    public static bool Unlocked(int requiredLevel, int petLevel) => requiredLevel <= petLevel;

    public const int TextAutoCastPossible = 14020;
    public const int TextAutoCastImpossible = 14021;
    public const int AutoFrameCount = 20;
    public const double AutoFrameSeconds = 0.04;

    public static int AutoFrameAt(double seconds) => (int)(seconds / AutoFrameSeconds) % AutoFrameCount;

    public static string AutoFramePath(int frame) => $"res://assets/ui/familiar_auto/auto_{frame:00}.png";

    public static string AutoCastLine(bool autoCast) => autoCast
        ? GameText.System(TextAutoCastPossible, "Automatic cast possible")
        : GameText.System(TextAutoCastImpossible, "Automatic cast impossible");

    public static int NextAutoCast(PetBarLayout layout, Func<int, bool> ready)
    {
        for (int i = 0; i < PetBarLayout.Size; i++)
            if (layout.IsAuto(i) && ready(layout[i])) return layout[i];
        return 0;
    }

    private const int UsableSlotA = 16;
    private const int UsableSlotB = 17;
    private const int SlotCodeBase = 100;

    public static bool FitsBarAsItem(int slotCode, int itemSkill) =>
        itemSkill != 0 && slotCode % SlotCodeBase is UsableSlotA or UsableSlotB;
}
