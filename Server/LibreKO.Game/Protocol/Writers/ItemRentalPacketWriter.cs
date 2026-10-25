using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

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

public sealed record RentalBoard(
    bool Own, bool Enabled, int Page, int Total, int MinDays, int MaxDays, int FeePerDay, int TaxPercent, long NowUnix,
    IReadOnlyList<RentalOffer> Offers);

public sealed record RentalOffer(RentalListingData Listing, bool Mine, int WholeDaysLeft, bool Rented);

public static class ItemRentalPacketWriter
{
    public static Packet Open(int npcId)
    {
        var packet = Sub(ItemRentalSub.Open);
        packet.WriteInt(npcId);
        return packet;
    }

    public static Packet Board(RentalBoard board)
    {
        var packet = Sub(ItemRentalSub.Board);
        packet.WriteByte(Flag(board.Own));
        packet.WriteByte(Flag(board.Enabled));
        packet.WriteUShort((ushort)board.Page);
        packet.WriteUShort((ushort)board.Total);
        packet.WriteByte((byte)board.MinDays);
        packet.WriteByte((byte)board.MaxDays);
        packet.WriteInt(board.FeePerDay);
        packet.WriteByte((byte)board.TaxPercent);
        packet.WriteLong(board.NowUnix);
        packet.WriteByte((byte)board.Offers.Count);
        foreach (var offer in board.Offers)
        {
            var listing = offer.Listing;
            packet.WriteInt(listing.Id);
            packet.WriteInt(listing.ItemId);
            packet.WriteShort(listing.Durability);
            packet.WriteSByteString(listing.OwnerName);
            packet.WriteInt(listing.DailyPrice);
            packet.WriteLong(new DateTimeOffset(listing.ListingEndsAt, TimeSpan.Zero).ToUnixTimeSeconds());
            packet.WriteLong(listing.RentedUntil is { } until ? new DateTimeOffset(until, TimeSpan.Zero).ToUnixTimeSeconds() : 0);
            packet.WriteLong(offer.Mine ? listing.Earnings : 0);
            packet.WriteInt(listing.Version);
            packet.WriteByte((byte)offer.WholeDaysLeft);
            packet.WriteByte(Flag(offer.Rented));
            packet.WriteByte(Flag(listing.Collected));
            packet.WriteByte(Flag(offer.Mine));
        }

        return packet;
    }

    public static Packet Result(bool ok, string message)
    {
        var packet = Sub(ItemRentalSub.Result);
        packet.WriteByte(Flag(ok));
        packet.WriteString(message);
        return packet;
    }

    private static byte Flag(bool on) => on ? (byte)1 : (byte)0;

    private static Packet Sub(ItemRentalSub sub)
    {
        var packet = new Packet(GameOpcodes.GS_ITEM_RENTAL);
        packet.WriteByte((byte)sub);
        return packet;
    }
}
