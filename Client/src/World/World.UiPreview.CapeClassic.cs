using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildCapeClassicUiPreview(int nation)
    {
        ItemData.EnsureLoaded();
        var me = Net.I.LastEnter;
        me.Name = "CapeTester2026"; me.Nation = nation; me.Race = nation == 1 ? 2 : 12;
        me.Class = nation == 1 ? 112 : 206; me.Hair = 0x5A3820;
        Net.I.SeedPreviewEnter(me);
        Net.I.SeedPreviewClan(PreviewClan(true, ClanTypes.Royal1));
        CapeInit(); ToggleCape();
        RemoveChild(_capeLayer); return _capeLayer;
    }
}
