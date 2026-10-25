using System.Collections.Generic;

namespace LibreKO.Domain;

public static class StorageDestination
{
    public static bool Joins(ItemSlot target, ItemSlot source) =>
        target.ItemId == source.ItemId
        && target.Flag == source.Flag
        && target.ExpiresAt == source.ExpiresAt
        && !target.IsLinked
        && !source.IsLinked;

    public static bool Fits(ItemSlot target, ItemSlot source, int count, bool stackable) => count > 0 &&
        (target.IsEmpty || stackable && Joins(target, source) && target.Count <= Inventory.StackMax - count);

    public static int Find(IReadOnlyList<ItemSlot> slots, ItemSlot source, int count, bool stackable) =>
        Find(slots, source, count, stackable, 0, 0);

    public static int Find(IReadOnlyList<ItemSlot> slots, ItemSlot source, int count, bool stackable, int pageStart, int pageSize)
    {
        if (stackable)
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty && Fits(slots[i], source, count, true)) return i;
        for (int i = System.Math.Max(0, pageStart); i < pageStart + pageSize && i < slots.Count; i++)
            if (slots[i].IsEmpty) return i;
        for (int i = 0; i < slots.Count; i++) if (slots[i].IsEmpty) return i;
        return -1;
    }
}
