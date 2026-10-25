namespace LibreKO.Domain;

public static class GenieSpirit
{
    public const int AskText = 31011;
    public const string AskFallback = "Not enough Knight Genie Point. Would you like to use 'Genie Spirit Potion?";
    public const int NoneText = 31012;
    public const string NoneFallback = "Not enough Knight Genie Point. To recharge point go to NPC to receive Genie Spirit Potion.";
    public const int UsedText = 31014;
    public const string UsedFallback = "Knight Genie point was used.";
    public const int NoSpirit = 0;

    private static readonly int[] Items = [900772000, 810305000, 810306000, 810378000, 811062000];

    public static int FirstOwned(Func<int, bool> owned) => Items.FirstOrDefault(owned);
}
