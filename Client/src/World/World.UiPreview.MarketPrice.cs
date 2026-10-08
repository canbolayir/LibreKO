using Godot;
using LibreKO.Domain;
namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildMarketPriceClassicUiPreview()
    {
        ItemData.EnsureLoaded(); SkillData.EnsureLoaded(); BuildItemTooltip(); BuildMarketPricePanel();
        _amountLayer = new CanvasLayer { Visible = false }; _marketPriceLayer.AddChild(_amountLayer);
        _itemTipLayer.Reparent(_marketPriceLayer, false);
        RemoveChild(_marketPriceLayer); return _marketPriceLayer;
    }
}
