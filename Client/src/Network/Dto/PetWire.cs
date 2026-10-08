using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct HatchedPet(int ItemId, int BagSlot, PetItemInfo Info);

public readonly record struct TransformedPet(HatchedPet Pet, int MaterialItemId, int MaterialSlot);
public readonly record struct PetFoodReply(bool Succeeded, int BagSlot, int ItemId, short CountLeft, short Increase);

public static class PetWire
{
    public const int ItemRecordBytes = 19;
    public const byte HatchSucceeded = 1;
    public const byte HatchNameTaken = 2;
    public const int NameTakenCode = -1;
    public const int MalformedReplyCode = int.MinValue;
    private const int HatchSuccessBytes = 17;
    private const int TransformTailBytes = 6;

    public static bool TryReadFood(Packet p, out PetFoodReply reply)
    {
        reply = default;
        if (p.RemainingBytes < 6) return false;
        byte result = p.ReadByte();
        int slot = p.ReadByte(), item = p.ReadInt();
        if (result > 1 || slot >= InventoryConstants.HaveMax || item <= 0) return false;
        if (result == 0) { reply = new(false, slot, item, 0, 0); return true; }
        if (p.RemainingBytes < 10) return false;
        short count = p.ReadShort(); p.ReadShort(); p.ReadInt(); short increase = p.ReadShort();
        if (count < 0 || count > Inventory.StackMax || increase < 0 || increase > PetSheet.MaxSatisfaction) return false;
        reply = new(true, slot, item, count, increase);
        return true;
    }

    public static ItemSlot ReadItemRecord(Packet p, out PetItemInfo? pet)
    {
        int itemId = p.ReadInt();
        short durability = p.ReadShort();
        short count = p.ReadShort();
        byte flag = p.ReadByte();
        p.ReadShort();
        int uniqueId = p.ReadInt();
        pet = null;
        if (uniqueId != 0)
        {
            string name = p.ReadString();
            int attack = p.ReadByte();
            int level = p.ReadByte();
            int expPercent = p.ReadUShort();
            int satisfaction = p.ReadShort();
            p.ReadByte();
            pet = new PetItemInfo(uniqueId, name, attack, level, expPercent, satisfaction);
        }
        p.ReadInt();
        return new ItemSlot { ItemId = itemId, Durability = durability, Count = count, Flag = flag, UniqueId = uniqueId };
    }

    public static PetSheet ReadSheet(Packet p)
    {
        var sheet = new PetSheet
        {
            Index = p.ReadInt(),
            Name = p.ReadString(),
            Class = p.ReadByte(),
            Level = p.ReadByte(),
            ExpPercent = p.ReadUShort(),
            MaxHp = p.ReadShort(),
            Hp = p.ReadShort(),
            MaxMp = p.ReadShort(),
            Mp = p.ReadShort(),
            Satisfaction = p.ReadShort(),
            Attack = p.ReadShort(),
            Defence = p.ReadShort(),
        };
        for (int i = 0; i < PetSheet.ResistanceCount; i++)
            sheet.Resists[i] = p.ReadByte();
        for (int i = 0; i < PetSheet.InventorySize && p.RemainingBytes >= ItemRecordBytes; i++)
            sheet.Items[i] = ReadItemRecord(p, out _);
        return sheet;
    }

    public static bool TryReadHatch(Packet p, out HatchedPet hatched, out int failure)
    {
        hatched = default;
        failure = MalformedReplyCode;
        if (p.RemainingBytes < 1) return false;
        byte result = p.ReadByte();
        if (result == HatchNameTaken)
        {
            failure = NameTakenCode;
            return false;
        }
        if (result == 0)
        {
            if (p.RemainingBytes >= 1) failure = p.ReadByte();
            return false;
        }
        if (result != HatchSucceeded) return false;
        if (p.RemainingBytes < HatchSuccessBytes) return false;
        int itemId = p.ReadInt();
        int bagSlot = p.ReadByte();
        int index = p.ReadInt();
        int nameLength = p.ReadShort();
        if (nameLength is < 1 or > 15 || p.RemainingBytes < nameLength + 6) return false;
        string name = System.Text.Encoding.ASCII.GetString(p.ReadBytes(nameLength));
        int attack = p.ReadByte();
        int level = p.ReadByte();
        int expPercent = p.ReadUShort();
        int satisfaction = p.ReadShort();
        if (itemId <= 0 || index <= 0 || bagSlot >= InventoryConstants.HaveMax || level == 0) return false;
        hatched = new HatchedPet(itemId, bagSlot, new PetItemInfo(index, name, attack, level, expPercent, satisfaction));
        failure = 0;
        return true;
    }

    public static bool TryReadTransform(Packet p, out TransformedPet transformed, out int failure)
    {
        transformed = default;
        if (!TryReadHatch(p, out var pet, out failure)) return false;
        if (p.RemainingBytes < TransformTailBytes)
        {
            failure = MalformedReplyCode;
            return false;
        }
        p.ReadByte();
        int materialItemId = p.ReadInt();
        int materialSlot = p.ReadByte();
        if (materialItemId <= 0 || materialSlot >= InventoryConstants.HaveMax || materialSlot == pet.BagSlot)
        { failure = MalformedReplyCode; return false; }
        transformed = new TransformedPet(pet, materialItemId, materialSlot);
        return true;
    }
}
