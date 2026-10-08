using System;
using Godot;
using LibreKO.Plugins;

namespace LibreKO;

/// <summary>Exposes the summoned familiar's appearance to UI renderers without retaining another live model.</summary>
public partial class FamiliarPortraitView : Control
{
    public GameNpcPortrait? Appearance { get; private set; }
    public event Action? AppearanceChanged;
    internal void SetAppearance(GameNpcPortrait? appearance)
    {
        if (Appearance?.AppearanceKey == appearance?.AppearanceKey) return;
        Appearance = appearance; AppearanceChanged?.Invoke();
    }
}
