namespace LibreKO.Network.Requests;

public interface IItemRentalRequests
{
    void Board(int npcId, RentalQuery query);
    void List(int npcId, byte bagSlot, int itemId, int days, int dailyPrice);
    void Rent(int npcId, RentalOffer offer, int days);
    void CollectEarnings(int npcId, int listingId);
    void CollectItem(int npcId, int listingId);
}

public sealed class ItemRentalRequests : IItemRentalRequests
{
    private readonly IPacketSink sink;

    public ItemRentalRequests(IPacketSink sink) => this.sink = sink;

    public void Board(int npcId, RentalQuery query)
    {
        var p = Request(ItemRentalRequest.Board, npcId);
        p.WriteByte((byte)(query.Own ? 1 : 0));
        p.WriteUShort((ushort)query.Page);
        p.WriteString(query.Text);
        p.WriteByte((byte)query.Category);
        p.WriteByte((byte)query.Class);
        p.WriteByte((byte)query.Sort);
        sink.Send(p);
    }

    public void List(int npcId, byte bagSlot, int itemId, int days, int dailyPrice)
    {
        var p = Request(ItemRentalRequest.List, npcId);
        p.WriteByte(bagSlot);
        p.WriteInt(itemId);
        p.WriteByte((byte)days);
        p.WriteInt(dailyPrice);
        sink.Send(p);
    }

    public void Rent(int npcId, RentalOffer offer, int days)
    {
        var p = Request(ItemRentalRequest.Rent, npcId);
        p.WriteInt(offer.Id);
        p.WriteByte((byte)days);
        p.WriteInt(offer.DailyPrice);
        p.WriteInt(offer.Version);
        sink.Send(p);
    }

    public void CollectEarnings(int npcId, int listingId)
    {
        var p = Request(ItemRentalRequest.CollectEarnings, npcId);
        p.WriteInt(listingId);
        sink.Send(p);
    }

    public void CollectItem(int npcId, int listingId)
    {
        var p = Request(ItemRentalRequest.CollectItem, npcId);
        p.WriteInt(listingId);
        sink.Send(p);
    }

    private static Packet Request(ItemRentalRequest sub, int npcId)
    {
        var p = new Packet(GameOpcodes.GS_ITEM_RENTAL);
        p.WriteByte((byte)sub);
        p.WriteInt(npcId);
        return p;
    }
}
