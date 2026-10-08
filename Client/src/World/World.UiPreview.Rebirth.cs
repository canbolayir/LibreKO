using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildRebirthClassicUiPreview(int nation)
    {
        ItemData.EnsureLoaded();
        var me = Net.I.LastEnter;
        me.Name = "RebirthTester2026"; me.Nation = nation;
        Net.I.SeedPreviewEnter(me);
        Sheet.SeedProgress(83, 0, 1);
        Sheet.SeedRebirth(4, 3, 1, 2, 1, 1);
        RebirthInit(); OpenRebirthPicker();
        RemoveChild(_rebirthLayer);
        return _rebirthLayer;
    }
}
