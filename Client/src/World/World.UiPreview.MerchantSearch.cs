using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildMerchantSearchClassicUiPreview()
    {
        ItemData.EnsureLoaded(); SkillData.EnsureLoaded(); BuildMerchantSearchPanel(); BuildMarketPricePanel();
        _whisperLayer = new CanvasLayer { Layer = 79 }; _merchantSearchLayer.AddChild(_whisperLayer);
        _marketPriceLayer.Reparent(_merchantSearchLayer, false); OnMerchantSearchOpen();
        RemoveChild(_merchantSearchLayer); return _merchantSearchLayer;
    }
}
