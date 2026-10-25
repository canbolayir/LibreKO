namespace LibreKO.Network;

public static class ItemCombineWire
{
    public static CombineReply ReadReply(Packet p)
    {
        byte result = p.ReadByte();
        short row = 0;
        byte slot = 0;
        if (ItemCombine.IsDone(result))
        {
            row = p.ReadShort();
            if (result == ItemCombine.Succeeded)
            {
                p.ReadInt();
                slot = p.ReadByte();
            }
        }
        return new CombineReply(result, row, slot);
    }
}
