using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildNationTransferClassicUiPreview(int nation)
    {
        ItemData.EnsureLoaded();
        var me = Net.I.LastEnter;
        me.Name = "AppearanceTester2026"; me.Nation = nation; me.Class = nation == 1 ? 112 : 206;
        me.Race = nation == 1 ? 2 : 12; me.Face = 0; me.Hair = 0x5A3820;
        Net.I.SeedPreviewEnter(me);
        NationTransferInit();
        RemoveChild(_transferLayer);
        return _transferLayer;
    }
}
