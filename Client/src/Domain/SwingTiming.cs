namespace LibreKO.Domain;

public static class SwingTiming
{
    public const float BareHandedSeconds = 1f;
    public const int AttackSpeedBuffType = 5;
    public const float ToolSwingSlowdown = 1.2f;
    private const float PercentScale = 100f;
    private const int FirstToolKind = 61;
    private const int LastToolKind = 63;

    public static float IntervalSeconds(int weaponDelay, int intervalPercent, float attackSpeed)
    {
        float seconds = BareHandedSeconds;
        if (weaponDelay > 0)
        {
            float percent = intervalPercent > 0 ? intervalPercent : PercentScale;
            seconds = weaponDelay / PercentScale * (percent / PercentScale);
        }
        return attackSpeed > 0f ? seconds / attackSpeed : seconds;
    }

    public static bool SetsAttackSpeed(int buffType, int attackSpeedPercent) =>
        buffType == AttackSpeedBuffType && attackSpeedPercent > 0;

    public static float BuffFactor(int attackSpeedPercent) => attackSpeedPercent / PercentScale;

    public static float AnimationRate(float attackSpeed, int rightKind, int leftKind) =>
        IsTool(rightKind) || IsTool(leftKind) ? attackSpeed / ToolSwingSlowdown : attackSpeed;

    private static bool IsTool(int kind) => kind is >= FirstToolKind and <= LastToolKind;
}
