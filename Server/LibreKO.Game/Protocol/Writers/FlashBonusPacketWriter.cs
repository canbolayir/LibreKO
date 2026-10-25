using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol.Writers;

public static class FlashBonusPacketWriter
{
    public const byte ActionStarted = 1;
    public const byte ActionEnded = 2;
    private const byte Unused = 0;

    public static Packet Started(FlashKind kind, int amount)
    {
        var packet = Header(ActionStarted, kind);
        packet.WriteInt(amount);
        return packet;
    }

    public static Packet Ended(FlashKind kind) => Header(ActionEnded, kind);

    private static Packet Header(byte action, FlashKind kind)
    {
        var packet = new Packet(GameOpcodes.GS_STORY);
        packet.WriteByte((byte)PreGameSubOpcode.FlashBonus);
        packet.WriteByte(action);
        packet.WriteByte((byte)kind);
        packet.WriteByte(Unused);
        return packet;
    }
}
