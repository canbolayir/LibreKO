using Godot;
namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildPowerUpStoreClassicUiPreview(string variant)
    {
        BuildItemTooltip(); var layer = BuildPowerUpStoreUiPreview(variant);
        _itemTipLayer.Reparent(layer, false); return layer;
    }
}
