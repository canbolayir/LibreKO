using LibreKO.Common.Domain.Entities.GameData;

namespace LibreKO.Game.Protocol;

public static class DropRoll
{
    public const int PercentScale = 10_000;
    public const int FirstItemId = 100_000_000;
    public const int FirstGroupId = 100;
    public const int CoinRollMinPercent = 70;
    public const int CoinRollMaxPercent = 100;
    private const int Hundred = 100;

    public static int Chance(int percent, int premiumBonus, int dropEventPercent)
    {
        var chance = premiumBonus > 0 ? percent + percent * premiumBonus / Hundred : percent;
        return dropEventPercent == 0 ? chance : (int)((long)chance * (Hundred + dropEventPercent) / Hundred);
    }

    public static bool Lands(int chance, Random random) => random.Next(1, PercentScale + 1) <= chance;

    public static ushort Coins(int goldDrop, int coinEventPercent, Random random)
    {
        if (goldDrop <= 0)
            return 0;

        var rolled = (long)goldDrop * random.Next(CoinRollMinPercent, CoinRollMaxPercent + 1) / Hundred;
        var coins = Math.Min(rolled, int.MaxValue);
        if (coinEventPercent != 0)
            coins = coins * (Hundred + coinEventPercent) / Hundred;
        return (ushort)Math.Clamp(coins, 0, ushort.MaxValue);
    }

    public static int ResolveItem(int dropValue, IReadOnlyDictionary<int, int[]> groups, Random random)
    {
        if (dropValue >= FirstItemId)
            return dropValue;

        if (dropValue < FirstGroupId || !groups.TryGetValue(dropValue, out var pool) || pool.Length == 0)
            return 0;

        return pool[random.Next(pool.Length)];
    }

    public static bool IsGroup(int dropValue) => dropValue is >= FirstGroupId and < FirstItemId;

    public static DropTally Tally(
        int goldDrop, NpcItemData? row, IReadOnlyDictionary<int, int[]> groups, int rolls,
        int premiumBonus, int dropEventPercent, int coinEventPercent, Func<int, bool> itemExists, Random random)
    {
        var items = new Dictionary<int, int>();
        long coinTotal = 0;
        var coinMin = int.MaxValue;
        var coinMax = 0;
        var itemless = 0;
        var drops = row?.GetDrops() ?? [];
        for (var roll = 0; roll < rolls; roll++)
        {
            int coins = Coins(goldDrop, coinEventPercent, random);
            coinTotal += coins;
            coinMin = Math.Min(coinMin, coins);
            coinMax = Math.Max(coinMax, coins);
            var dropped = false;
            foreach (var (value, percent) in drops)
            {
                if (value <= 0 || percent <= 0 || !Lands(Chance(percent, premiumBonus, dropEventPercent), random))
                    continue;

                var itemId = ResolveItem(value, groups, random);
                if (itemId <= 0 || !itemExists(itemId))
                    continue;

                items[itemId] = items.GetValueOrDefault(itemId) + 1;
                dropped = true;
            }

            if (!dropped)
                itemless++;
        }

        return new DropTally(coinTotal, rolls > 0 ? coinMin : 0, coinMax, itemless, items);
    }
}

public sealed record DropTally(long CoinTotal, int CoinMin, int CoinMax, int ItemlessKills, IReadOnlyDictionary<int, int> Items);
