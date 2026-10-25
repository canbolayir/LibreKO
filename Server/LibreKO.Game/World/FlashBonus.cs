namespace LibreKO.Game.World;

public enum FlashKind : byte
{
    None = 0,
    Drop = 1,
    Exp = 2,
    Contribution = 3,
}

public static class FlashBonusRules
{
    public const int PercentStep = 10;
    public const int PercentCap = 100;
    public const int ContributionStep = 1;
    public const int ContributionCap = 10;
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);

    public static FlashKind KindOf(int specialAmount) => specialAmount switch
    {
        (int)FlashKind.Drop => FlashKind.Drop,
        (int)FlashKind.Exp => FlashKind.Exp,
        (int)FlashKind.Contribution => FlashKind.Contribution,
        _ => FlashKind.None,
    };

    public static int Step(FlashKind kind) => kind == FlashKind.Contribution ? ContributionStep : PercentStep;

    public static int Cap(FlashKind kind) => kind == FlashKind.Contribution ? ContributionCap : PercentCap;
}

public sealed class FlashBonusState
{
    private readonly object gate = new();

    public FlashKind Kind { get; private set; }
    public int Amount { get; private set; }
    public DateTime EndsAtUtc { get; private set; }

    public int Bonus(FlashKind kind, DateTime nowUtc)
    {
        lock (gate) return Kind == kind && kind != FlashKind.None && nowUtc < EndsAtUtc ? Amount : 0;
    }

    public bool CanAdd(FlashKind kind, DateTime nowUtc)
    {
        lock (gate) return !(Kind == kind && nowUtc < EndsAtUtc && Amount >= FlashBonusRules.Cap(kind));
    }

    public FlashKind Add(FlashKind kind, DateTime nowUtc)
    {
        lock (gate)
        {
            var replaced = Kind != FlashKind.None && Kind != kind && nowUtc < EndsAtUtc ? Kind : FlashKind.None;
            if (Kind != kind || nowUtc >= EndsAtUtc)
            {
                Kind = kind;
                Amount = 0;
                EndsAtUtc = nowUtc + FlashBonusRules.Duration;
            }
            Amount = Math.Min(FlashBonusRules.Cap(kind), Amount + FlashBonusRules.Step(kind));
            return replaced;
        }
    }

    public FlashKind TakeExpired(DateTime nowUtc)
    {
        lock (gate)
        {
            if (Kind == FlashKind.None || nowUtc < EndsAtUtc) return FlashKind.None;
            var ended = Kind;
            Kind = FlashKind.None;
            Amount = 0;
            return ended;
        }
    }
}
