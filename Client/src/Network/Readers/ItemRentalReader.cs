namespace LibreKO.Network.Readers;

internal sealed class ItemRentalReader : IPacketReader
{
    private readonly ItemRentalSession rental;

    public ItemRentalReader(ItemRentalSession rental)
    {
        this.rental = rental;
    }

    public void Register(PacketDispatcher dispatcher)
    {
        dispatcher.On(GameOpcodes.GS_ITEM_RENTAL, ItemRentalSub.Open, p => rental.RaiseOpen(p.ReadInt()));
        dispatcher.On(GameOpcodes.GS_ITEM_RENTAL, ItemRentalSub.Board, p => rental.RaiseBoard(ItemRentalWire.ReadBoard(p)));
        dispatcher.On(GameOpcodes.GS_ITEM_RENTAL, ItemRentalSub.Result, p => rental.RaiseResult(p.ReadByte() == 1, p.ReadString()));
    }
}
