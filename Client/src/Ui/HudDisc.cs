using Godot;

namespace LibreKO;

// A translucent StyleBoxFlat rounded to a circle seams down the middle (AA halves blend twice).
internal sealed partial class HudDisc : Control
{
    internal const float GlyphInset = 0.19f;
    private static readonly Color HoverGlyph = new(1.18f, 1.18f, 1.18f);
    private static readonly string[] FlatStates = { "normal", "hover", "pressed", "focus", "disabled" };
    private const float PaintedHoverLighten = 0.14f;
    private const float PaintedHeldDarken = 0.18f;
    private static readonly Color IdleFill = new(0.012f, 0.014f, 0.018f, 0.46f);
    private static readonly Color HoverFill = new(0.055f, 0.060f, 0.068f, 0.86f);
    private static readonly Color HeldFill = new(0.025f, 0.028f, 0.034f, 0.94f);
    private static readonly Color Ring = new(0.62f, 0.66f, 0.70f, 0.58f);
    private static readonly Color Halo = new(0f, 0f, 0f, 0.20f);

    private bool hover;
    private bool held;
    private Color idleColor = IdleFill;
    private Color hoverColor = HoverFill;
    private Color heldColor = HeldFill;

    public bool Hover { set { if (hover != value) { hover = value; QueueRedraw(); } } }

    public bool Held { set { if (held != value) { held = value; QueueRedraw(); } } }

    public override void _Draw()
    {
        Vector2 c = Size * 0.5f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
        DrawCircle(c, r, Halo, true, -1f, true);
        DrawCircle(c, r - 1.5f, held ? heldColor : hover ? hoverColor : idleColor, true, -1f, true);
        if (hover || held)
            DrawArc(c, r - 2f, 0f, Mathf.Tau, 48, Ring, 1f, true);
    }

    internal void Paint(Color color)
    {
        idleColor = color;
        hoverColor = color.Lightened(PaintedHoverLighten);
        heldColor = color.Darkened(PaintedHeldDarken);
        QueueRedraw();
    }

    internal static HudDisc Behind(Button button)
    {
        var disc = new HudDisc { MouseFilter = Control.MouseFilterEnum.Ignore };
        disc.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(disc);
        button.MouseEntered += () => disc.Hover = true;
        button.MouseExited += () => disc.Hover = false;
        button.ButtonDown += () => disc.Held = true;
        button.ButtonUp += () => disc.Held = false;
        return disc;
    }

    internal static Button IconButton(string iconId, string tooltip, Action pressed, Color glyph, out TextureRect image,
        float glyphInset = GlyphInset, Color? disc = null)
    {
        float size = HudPlacement.HudLauncherButtonSize;
        var button = new Button
        {
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(size, size),
        };
        var flat = new StyleBoxEmpty();
        foreach (string state in FlatStates) button.AddThemeStyleboxOverride(state, flat);
        if (Platform.PointerUi)
        {
            var backing = Behind(button);
            if (disc is { } color) backing.Paint(color);
        }

        // Button draws an expand_icon a few px off-centre; a full-rect child centres it exactly.
        var icon = new TextureRect
        {
            Texture = UiIcons.Get(iconId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SelfModulate = glyph,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        float inset = size * glyphInset;
        icon.OffsetLeft = icon.OffsetTop = inset;
        icon.OffsetRight = icon.OffsetBottom = -inset;
        button.AddChild(icon);
        button.MouseEntered += () => icon.Modulate = HoverGlyph;
        button.MouseExited += () => icon.Modulate = Colors.White;
        button.Pressed += pressed;
        image = icon;
        return button;
    }
}
