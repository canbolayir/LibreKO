using LibreKO.Common.Domain.Services;

namespace LibreKO.Game.World;

public enum RateEvent
{
    Exp,
    Coin,
    Np,
    Drop,
}

public static class RateEvents
{
    private const int ExpResource = 197;
    private const int CoinResource = 187;
    private const int NpResource = 246;
    private const int DropResource = 281;
    private const string AmountToken = "%d";

    public static readonly RateEvent[] All = [RateEvent.Exp, RateEvent.Coin, RateEvent.Np, RateEvent.Drop];

    public static string Title(RateEvent kind) => kind switch
    {
        RateEvent.Exp => "Exp Event",
        RateEvent.Coin => "Noah Event",
        RateEvent.Np => "NP Event",
        _ => "Drop Event",
    };

    public static int ResourceId(RateEvent kind) => kind switch
    {
        RateEvent.Exp => ExpResource,
        RateEvent.Coin => CoinResource,
        RateEvent.Np => NpResource,
        _ => DropResource,
    };

    public static byte Amount(TimeWeatherBroadcastService rates, RateEvent kind) => kind switch
    {
        RateEvent.Exp => rates.ExpEventAmount,
        RateEvent.Coin => rates.CoinEventAmount,
        RateEvent.Np => rates.NpEventAmount,
        _ => rates.DropEventAmount,
    };

    public static void Set(TimeWeatherBroadcastService rates, RateEvent kind, byte amount)
    {
        switch (kind)
        {
            case RateEvent.Exp: rates.ExpEventAmount = amount; break;
            case RateEvent.Coin: rates.CoinEventAmount = amount; break;
            case RateEvent.Np: rates.NpEventAmount = amount; break;
            default: rates.DropEventAmount = amount; break;
        }
    }

    public static string Message(IGameDataService data, RateEvent kind, int amount) =>
        (data.GetServerResource(ResourceId(kind)) ?? $"{Title(kind)} +{AmountToken}%").Replace(AmountToken, amount.ToString());

    public static List<(string Title, string Message)> Active(TimeWeatherBroadcastService rates, IGameDataService data) =>
        All.Where(kind => Amount(rates, kind) > 0)
            .Select(kind => (Title(kind), Message(data, kind, Amount(rates, kind))))
            .ToList();
}
