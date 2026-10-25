using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class UtcMoviePacketWriter
{
    public const byte SubMovie = 2;
    public const byte Shown = 1;

    public static Packet Movie(int movieId)
    {
        var packet = new Packet(GameOpcodes.GS_UTC_MOVIE);
        packet.WriteByte(SubMovie);
        packet.WriteByte(Shown);
        packet.WriteInt(0);
        packet.WriteInt(movieId);
        return packet;
    }
}
