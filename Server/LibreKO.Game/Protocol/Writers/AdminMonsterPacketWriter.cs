using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class AdminMonsterPacketWriter
{
    public readonly record struct SheetValue(byte Id, string Label, int Value, int Min, int Max);

    public readonly record struct DropSlot(int Value, short Percent, string Label);

    public readonly record struct Sheet(
        bool IsMonster, int ProtoId, string Name, byte NpcType, int SpawnRow, int Alive,
        IReadOnlyList<SheetValue> Values, short DropRow, bool DropRowExists, int DropRowSharedBy,
        IReadOnlyList<DropSlot> Drops, byte DropEventPercent, byte CoinEventPercent);

    public readonly record struct Simulation(
        bool IsMonster, int ProtoId, int Rolls, long CoinTotal, int CoinMin, int CoinMax, int ItemlessKills,
        IReadOnlyList<(int ItemId, int Times)> Items);

    public static Packet MonsterSheet(byte sub, Sheet sheet)
    {
        var packet = Sub(sub);
        packet.WriteByte(Flag(sheet.IsMonster));
        packet.WriteInt(sheet.ProtoId);
        packet.WriteSByteString(sheet.Name);
        packet.WriteByte(sheet.NpcType);
        packet.WriteInt(sheet.SpawnRow);
        packet.WriteInt(sheet.Alive);
        packet.WriteByte((byte)sheet.Values.Count);
        foreach (var value in sheet.Values)
        {
            packet.WriteByte(value.Id);
            packet.WriteSByteString(value.Label);
            packet.WriteInt(value.Value);
            packet.WriteInt(value.Min);
            packet.WriteInt(value.Max);
        }

        packet.WriteShort(sheet.DropRow);
        packet.WriteByte(Flag(sheet.DropRowExists));
        packet.WriteUShort((ushort)Math.Min(sheet.DropRowSharedBy, ushort.MaxValue));
        packet.WriteByte((byte)sheet.Drops.Count);
        foreach (var drop in sheet.Drops)
        {
            packet.WriteInt(drop.Value);
            packet.WriteShort(drop.Percent);
            packet.WriteSByteString(drop.Label);
        }

        packet.WriteByte(sheet.DropEventPercent);
        packet.WriteByte(sheet.CoinEventPercent);
        return packet;
    }

    public static Packet ItemGroup(byte sub, int group, bool exists, IReadOnlyList<int> items)
    {
        var packet = Sub(sub);
        packet.WriteInt(group);
        packet.WriteByte(Flag(exists));
        packet.WriteUShort((ushort)items.Count);
        foreach (var item in items)
            packet.WriteInt(item);
        return packet;
    }

    public readonly record struct GroupRow(int Group, int ItemCount, IReadOnlyList<int> Samples);

    public static Packet ItemGroups(byte sub, int total, IReadOnlyList<GroupRow> rows)
    {
        var packet = Sub(sub);
        packet.WriteUShort((ushort)Math.Min(total, ushort.MaxValue));
        packet.WriteByte((byte)rows.Count);
        foreach (var row in rows)
        {
            packet.WriteInt(row.Group);
            packet.WriteUShort((ushort)Math.Min(row.ItemCount, ushort.MaxValue));
            packet.WriteByte((byte)row.Samples.Count);
            foreach (var item in row.Samples)
                packet.WriteInt(item);
        }

        return packet;
    }

    public static Packet DropSimulation(byte sub, Simulation result)
    {
        var packet = Sub(sub);
        packet.WriteByte(Flag(result.IsMonster));
        packet.WriteInt(result.ProtoId);
        packet.WriteInt(result.Rolls);
        packet.WriteLong(result.CoinTotal);
        packet.WriteInt(result.CoinMin);
        packet.WriteInt(result.CoinMax);
        packet.WriteInt(result.ItemlessKills);
        packet.WriteUShort((ushort)result.Items.Count);
        foreach (var (itemId, times) in result.Items)
        {
            packet.WriteInt(itemId);
            packet.WriteInt(times);
        }

        return packet;
    }

    private static byte Flag(bool on) => on ? AdminPanelPacketWriter.Granted : AdminPanelPacketWriter.Denied;

    private static Packet Sub(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteByte(sub);
        return packet;
    }
}
