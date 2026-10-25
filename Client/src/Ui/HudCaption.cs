using Godot;

namespace LibreKO;

internal static class HudCaption
{
    private const int FontSize = 10;
    private const int OutlineSize = 3;
    private const float Gap = 1f;
    private const float Height = 13f;
    internal const float Space = Gap + Height;

    internal static void Attach(Control icon, string text)
    {
        if (Platform.TouchUi || text.Length == 0) return;
        var label = UiTheme.Text(text, FontSize, UiTheme.TextHi, HorizontalAlignment.Center);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", OutlineSize);
        label.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        label.OffsetTop = Gap;
        label.OffsetBottom = Gap + Height;
        label.GrowHorizontal = Control.GrowDirection.Both;
        label.Visible = Config.IconCaptions;
        icon.AddChild(label);
        void Sync()
        {
            if (GodotObject.IsInstanceValid(label)) label.Visible = Config.IconCaptions;
            else Config.IconCaptionsChanged -= Sync;
        }
        Config.IconCaptionsChanged += Sync;
    }
}
