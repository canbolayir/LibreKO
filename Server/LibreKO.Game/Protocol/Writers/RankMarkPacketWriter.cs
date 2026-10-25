using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol.Writers;

public static class RankMarkPacketWriter
{
    public static Packet Own(RankMarks marks)
    {
        var packet = new Packet(GameOpcodes.GS_RANK_MARKS);
        WriteBlocks(packet, marks);
        return packet;
    }

    public static void WriteBlocks(Packet packet, RankMarks marks)
    {
        var boards = marks.Boards;
        packet.WriteByte((byte)Math.Min(boards.Count, byte.MaxValue));
        foreach (var (type, bronze, silver, gold) in boards.Take(byte.MaxValue))
        {
            packet.WriteUShort(type);
            packet.WriteShort(Count(bronze));
            packet.WriteShort(Count(silver));
            packet.WriteShort(Count(gold));
        }

        var ribbons = marks.Ribbons;
        packet.WriteByte((byte)Math.Min(ribbons.Count, byte.MaxValue));
        foreach (var (id, count) in ribbons.Take(byte.MaxValue))
        {
            packet.WriteUShort(id);
            packet.WriteUShort(count);
        }
    }

    private static short Count(ushort count) => (short)Math.Min(count, (ushort)short.MaxValue);
}
