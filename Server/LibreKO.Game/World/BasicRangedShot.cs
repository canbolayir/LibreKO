namespace LibreKO.Game.World;

public static class BasicRangedShot
{
    public const int KnightRoyaleArchery = 490356;
    private const int ClassSkillSpan = 1000;
    private const int ShotIndex = 3;
    private static readonly int[] ArcherClasses = [102, 107, 108, 202, 207, 208];

    public static bool Is(int skillId) =>
        skillId == KnightRoyaleArchery
        || (skillId % ClassSkillSpan == ShotIndex && Array.IndexOf(ArcherClasses, skillId / ClassSkillSpan) >= 0);
}
