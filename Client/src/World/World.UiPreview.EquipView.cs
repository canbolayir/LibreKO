using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;
namespace LibreKO;
public partial class World
{
    internal CanvasLayer BuildEquipViewClassicUiPreview()
    {
        ItemData.EnsureLoaded(); BuildItemTooltip(); EquipViewInit();
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        Inv[InventoryConstants.InventoryStart] = new ItemSlot { ItemId = 389015000, Count = 100, Durability = 1 };
        _itemTipLayer.Reparent(_equipViewLayer, false); RemoveChild(_equipViewLayer); return _equipViewLayer;
    }
    internal static Net.EquipmentView EquipViewPreviewSnapshot(string name, int nation, bool empty = false, bool maximum = false)
    {
        var worn = new List<(int Slot, int ItemId, short Durability, byte Flag)>();
        if (!empty)
        {
            int[] gear = { 1310610106, 208003000, 1310610106, 330310000, 208001000, 0, 156210008, 0, 136710000, 320410011, 208002000, 320410011, 208004000, 208005000 };
            for (int i = 0; i < InventoryConstants.SlotMax; i++) worn.Add((i, gear[i], (short)(i == 6 ? 0 : 12000), (byte)(i == 6 ? ItemFlag.Sealed : 0)));
            int[] positions = { 8, 1, 7, 2, 4, 3, 9, 0, 5 };
            int[] codes = { 112, 107, 111, 100, 105, 100, 113, 110, 114 };
            for (int i = 0; i < positions.Length; i++)
            {
                int id = ItemData.All().Where(item => item.Slot == codes[i] && ResourceLoader.Exists($"res://assets/items/icons/{(ItemData.ExtFor(item.Id) is { Icon: > 0 } ext ? ext.Icon : item.Icon)}.png")).OrderBy(item => item.Id).FirstOrDefault()?.Id ?? 0;
                worn.Add((InventoryConstants.CospreStart + positions[i], id, 1, 0));
            }
        }
        return new Net.EquipmentView(name, nation == 1 ? 101 : 201, nation == 1 ? 1 : 11, 0, 0, 83, 5, nation,
            maximum ? 32767 : 7836, maximum ? 32767 : 7696, 255, maximum ? 255 : 13, 255, 10, 60, 8, 50, 0, 50, 0,
            maximum ? 32767 : 2103, maximum ? 32767 : 238, 120, 90, 140, 50, 130, 170, worn);
    }
}
