using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class PlayerInspectPacketWriter
{
    public const byte DetailAccepted = 1;
    public const byte DetailNoUser = 2;
    public const short NoParty = -1;

    public const int RankPlaceCount = 7;
    public const int EquipmentSlotCount =
        InventoryConstants.SlotMax + InventoryConstants.CospreWireMax;

    public readonly record struct WornItem(int ItemId, short Durability, byte Flag, int ExpiresAt);

    public readonly record struct Clan(
        short Id, short MarkVersion, byte Flag, byte Grade, string Name, string Chief, byte Ranking);

    public static readonly Clan NoClan = new(0, 0, 0, 0, "", "", 0);

    public readonly record struct Detail(
        string Name,
        byte Level,
        short Class,
        int Loyalty,
        int MonthlyLoyalty,
        Clan Clan,
        byte RebirthLevel,
        short PartyMarker,
        IReadOnlyList<int> RankPlaces,
        World.RankMarks Marks);

    public readonly record struct Equipment(
        string Name,
        short Class,
        byte Race,
        byte Face,
        int Hair,
        byte Level,
        byte RebirthLevel,
        byte Nation,
        short MaxHp,
        short MaxMp,
        byte Strength, byte StrengthBonus,
        byte Stamina, byte StaminaBonus,
        byte Dexterity, byte DexterityBonus,
        byte Intelligence, byte IntelligenceBonus,
        byte Magic, byte MagicBonus,
        short Attack,
        short Defence,
        short FireResist, short ColdResist, short LightningResist,
        short MagicResist, short DiseaseResist, short PoisonResist,
        IReadOnlyList<WornItem> Worn);

    public static Packet DetailRefused()
    {
        var packet = Sub(PlayerInspectSubOpcode.Detail);
        packet.WriteByte(DetailNoUser);
        return packet;
    }

    public static Packet UserDetail(Detail detail)
    {
        var packet = Sub(PlayerInspectSubOpcode.Detail);
        packet.WriteByte(DetailAccepted);
        packet.WriteSByteString(detail.Name);
        packet.WriteByte(detail.Level);
        packet.WriteShort(detail.Class);
        packet.WriteInt(detail.Loyalty);
        packet.WriteInt(detail.MonthlyLoyalty);
        packet.WriteByte(0);

        packet.WriteShort(detail.Clan.Id);
        packet.WriteShort(detail.Clan.MarkVersion);
        packet.WriteByte(detail.Clan.Flag);
        packet.WriteByte(detail.Clan.Grade);
        packet.WriteSByteString(detail.Clan.Name);
        packet.WriteSByteString(detail.Clan.Chief);
        packet.WriteByte(detail.Clan.Ranking);

        packet.WriteByte(detail.RebirthLevel);
        packet.WriteShort(detail.PartyMarker);

        for (var i = 0; i < RankPlaceCount; i++)
            packet.WriteInt(i < detail.RankPlaces.Count ? detail.RankPlaces[i] : 0);

        RankMarkPacketWriter.WriteBlocks(packet, detail.Marks);
        return packet;
    }

    public static Packet EquipmentRefused(EquipmentViewResult result)
    {
        var packet = Sub(PlayerInspectSubOpcode.Equipment);
        packet.WriteShort((short)result);
        return packet;
    }

    public static Packet EquipmentView(Equipment equipment)
    {
        var packet = Sub(PlayerInspectSubOpcode.Equipment);
        packet.WriteShort((short)EquipmentViewResult.Accepted);
        packet.WriteSByteString(equipment.Name);
        packet.WriteShort(equipment.Class);
        packet.WriteByte(equipment.Race);
        packet.WriteByte(equipment.Face);
        packet.WriteInt(equipment.Hair);
        packet.WriteByte(equipment.Level);
        packet.WriteByte(equipment.RebirthLevel);
        packet.WriteByte(equipment.Nation);
        packet.WriteShort(equipment.MaxHp);
        packet.WriteShort(equipment.MaxMp);
        packet.WriteShort(0);

        packet.WriteByte(equipment.Strength);
        packet.WriteByte(equipment.StrengthBonus);
        packet.WriteByte(equipment.Stamina);
        packet.WriteByte(equipment.StaminaBonus);
        packet.WriteByte(equipment.Dexterity);
        packet.WriteByte(equipment.DexterityBonus);
        packet.WriteByte(equipment.Intelligence);
        packet.WriteByte(equipment.IntelligenceBonus);
        packet.WriteByte(equipment.Magic);
        packet.WriteByte(equipment.MagicBonus);

        packet.WriteShort(equipment.Attack);
        packet.WriteShort(equipment.Defence);
        packet.WriteShort(equipment.FireResist);
        packet.WriteShort(equipment.ColdResist);
        packet.WriteShort(equipment.LightningResist);
        packet.WriteShort(equipment.MagicResist);
        packet.WriteShort(equipment.DiseaseResist);
        packet.WriteShort(equipment.PoisonResist);

        for (var i = 0; i < EquipmentSlotCount; i++)
        {
            var worn = i < equipment.Worn.Count ? equipment.Worn[i] : default;
            packet.WriteInt(worn.ItemId);
            packet.WriteShort(worn.Durability);
            packet.WriteByte(worn.Flag);
            packet.WriteInt(worn.ExpiresAt);
        }

        packet.WriteByte(0);
        return packet;
    }

    private static Packet Sub(PlayerInspectSubOpcode sub)
    {
        var packet = new Packet(GameOpcodes.GS_USER_INFO);
        packet.WriteByte((byte)sub);
        return packet;
    }
}
