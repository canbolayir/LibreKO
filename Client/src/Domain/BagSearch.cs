using System.Collections.Generic;

namespace LibreKO.Domain;

public static class BagSearch
{
    public static bool[] BagsHoldingHits(IReadOnlySet<int> hits)
    {
        var bags = new bool[InventoryConstants.BagSlotMax];
        foreach (int slot in hits)
        {
            int position = slot - InventoryConstants.MagicBagStart;
            if (position < 0 || position >= InventoryConstants.MagicBagTotal) continue;
            bags[InventoryConstants.BagIndexForMagicBagPosition(position)] = true;
        }
        return bags;
    }

    public static bool Searchable(int slot) =>
        InventoryConstants.IsGridSlot(slot)
        || (slot >= InventoryConstants.MagicBagStart && slot < InventoryConstants.MagicBagStart + InventoryConstants.MagicBagTotal);
}
