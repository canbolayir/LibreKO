namespace LibreKO.Domain;

public static class ItemRentalRules
{
    public const int SecondsPerDay = 86_400;
    private const int UntradeableRace = 20;
    private const int LastRentableSlot = 14;

    private static readonly (int From, int To)[] RentableKinds = { (11, 94), (110, 110), (140, 140), (181, 181), (200, 240) };

    public static bool Rentable(ItemData.Item item, ItemSlot slot) =>
        item.Countable == 0
        && slot.Count <= 1
        && slot.IsTradable
        && slot.ExpiresAt == 0
        && item.Race != UntradeableRace
        && item.Slot <= LastRentableSlot
        && Array.Exists(RentableKinds, kind => item.Kind >= kind.From && item.Kind <= kind.To);

    public static long Fee(int days, int feePerDay) => (long)days * feePerDay;

    public static long Cost(int days, int dailyPrice) => (long)days * dailyPrice;

    public static string Status(RentalOffer offer, long nowUnix) =>
        offer.Collected ? "Original collected"
        : offer.Rented ? "Currently rented"
        : offer.ListingEndsUnix <= nowUnix ? "Listing ended"
        : offer.WholeDaysLeft == 0 ? "No full rental day remains"
        : "Available";

    public static bool CanRent(RentalBoard board, RentalOffer offer) =>
        board.Enabled && !offer.Collected && !offer.Rented && !offer.Mine && offer.WholeDaysLeft > 0;

    public static bool CanCollectItem(RentalOffer offer) => offer.Mine && !offer.Collected && !offer.Rented;
}
