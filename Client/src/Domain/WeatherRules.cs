namespace LibreKO.Domain;

public static class WeatherRules
{
    public const int Fine = 1;
    public const int Rain = 2;
    public const int Snow = 3;
    public const int Unchanged = 6;
    public const int MinAmount = 20;
    public const int FineMaxAmount = 100;
    public const int OtherMaxAmount = 70;

    private const int NoWeatherZone = 86;
    private const int PetalsWire = 4;
    private const int PetalRainWire = 5;
    private const int EmbersWire = 7;
    private const float FineLightFloor = 0.5f;
    private const float RainLightFloor = 0.25f;
    private const float LightMin = 0.25f;
    private const float LightMax = 1f;
    private const float AmountScale = 100f;

    private static readonly int[] FineZones = { 31, 35, 45, 46, 61, 62, 63, 64, 75, 85, 87, 89 };

    public static bool Ignored(int zone) => zone == NoWeatherZone;

    public static int Type(int zone, int type)
    {
        if (System.Array.IndexOf(FineZones, zone) >= 0) return Fine;
        return type switch
        {
            PetalRainWire => Rain,
            PetalsWire or EmbersWire => Fine,
            _ => type,
        };
    }

    public static int Amount(int type, int amount) =>
        System.Math.Clamp(amount, MinAmount, type == Fine ? FineMaxAmount : OtherMaxAmount);

    public static float Light(int type, int amount)
    {
        float clear = 1f - amount / AmountScale;
        float light = type switch
        {
            Fine => FineLightFloor + (1f - FineLightFloor) * clear,
            Rain or Snow => RainLightFloor + (1f - RainLightFloor) * clear,
            _ => LightMax,
        };
        return System.Math.Clamp(light, LightMin, LightMax);
    }

    public static float RelativeLight(int type, int amount) => Light(type, amount) / Light(type, MinAmount);
}
