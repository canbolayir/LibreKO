namespace LibreKO.Network;

public enum ItemRentalSub : byte
{
    Open = 1,
    Board = 2,
    Result = 3,
}

public enum ItemRentalRequest : byte
{
    Board = 1,
    List = 2,
    Rent = 3,
    CollectEarnings = 4,
    CollectItem = 5,
}

public enum RentalCategory : byte
{
    Any = 0,
    Weapon = 1,
    Armor = 2,
    Accessory = 3,
}

public enum RentalClass : byte
{
    Any = 0,
    Warrior = 1,
    Rogue = 2,
    Mage = 3,
    Priest = 4,
}

public enum RentalSort : byte
{
    Newest = 0,
    PriceLow = 1,
    PriceHigh = 2,
}

public sealed record RentalQuery(bool Own, int Page, string Text, RentalCategory Category, RentalClass Class, RentalSort Sort);

public sealed record RentalOffer(
    int Id, int ItemId, short Durability, string Owner, int DailyPrice, long ListingEndsUnix, long RentedUntilUnix,
    long Earnings, int Version, int WholeDaysLeft, bool Rented, bool Collected, bool Mine);

public sealed record RentalBoard(
    bool Own, bool Enabled, int Page, int Total, int MinDays, int MaxDays, int FeePerDay, int TaxPercent, long NowUnix,
    IReadOnlyList<RentalOffer> Offers)
{
    public const int PageSize = 20;

    public int Pages => System.Math.Max(1, (Total + PageSize - 1) / PageSize);
}

public static class ItemRentalWire
{
    public static RentalBoard ReadBoard(Packet p)
    {
        bool own = p.ReadByte() == 1;
        bool enabled = p.ReadByte() == 1;
        int page = p.ReadUShort();
        int total = p.ReadUShort();
        int minDays = p.ReadByte();
        int maxDays = p.ReadByte();
        int fee = p.ReadInt();
        int tax = p.ReadByte();
        long now = p.ReadLong();
        int count = p.ReadByte();
        var offers = new List<RentalOffer>(count);
        for (int i = 0; i < count; i++)
            offers.Add(new RentalOffer(p.ReadInt(), p.ReadInt(), p.ReadShort(), p.ReadSByteString(), p.ReadInt(), p.ReadLong(), p.ReadLong(),
                p.ReadLong(), p.ReadInt(), p.ReadByte(), p.ReadByte() == 1, p.ReadByte() == 1, p.ReadByte() == 1));
        return new RentalBoard(own, enabled, page, total, minDays, maxDays, fee, tax, now, offers);
    }
}
