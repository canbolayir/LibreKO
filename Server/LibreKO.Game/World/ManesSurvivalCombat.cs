namespace LibreKO.Game.World;

public static class ManesSurvivalCombat
{
    public const int SkillHitBase = 100;
    public const int MonsterDamageDivisor = 10;
    private const int PercentScale = 100;
    private const int ArmourScale = 1000;
    private const int MinimumHit = 1;

    public static int MeleeSkill(short hit) => SkillHitBase * Math.Max((short)0, hit) / PercentScale;

    public static int Dealt(UserSession attacker, int damage)
    {
        if (damage <= 0 || attacker.ManesSurvival is not { } entry)
            return damage;
        long scaled = (long)damage * attacker.AttackAmount / PercentScale * (PercentScale + entry.AttackUp) / PercentScale;
        return (int)Math.Clamp(scaled, MinimumHit, int.MaxValue);
    }

    public static int Taken(UserSession victim, int damage)
    {
        if (damage <= 0 || victim.ManesSurvival is not { } entry)
            return damage;
        long armour = BuffArmour(victim);
        long scaled = (long)damage * Math.Max(0, PercentScale - entry.AttackDown) / PercentScale
            * ArmourScale / (ArmourScale + armour);
        return (int)Math.Clamp(scaled, MinimumHit, int.MaxValue);
    }

    public static int Hit(UserSession attacker, UserSession victim, int damage) => Taken(victim, Dealt(attacker, damage));

    public static int FromMonster(UserSession victim, int damage) =>
        victim.ManesSurvival == null || damage <= 0 ? damage : Taken(victim, Math.Max(MinimumHit, damage / MonsterDamageDivisor));

    private static long BuffArmour(UserSession victim)
    {
        var now = DateTime.UtcNow.Ticks;
        return victim.ActiveBuffs.Values.Where(buff => buff.ExpireTicks > now && buff.BonusAc > 0).Sum(buff => (long)buff.BonusAc);
    }
}
