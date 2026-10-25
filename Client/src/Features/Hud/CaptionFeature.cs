using Godot;

namespace LibreKO.Features.Hud;

internal sealed class CaptionFeature : IFeature
{
    private readonly IWorldContext context;

    public CaptionFeature(IWorldContext context)
    {
        this.context = context;
    }

    internal const int TitleFontSize = 26;
    private const int SubtitleFontSize = 20;
    private const float SubtitleDrop = 50f;
    private const char SubtitleSeparator = '/';
    internal const double FadeOut = 1.0;

    private Control? banner;
    private Label title = null!;
    private Label subtitle = null!;
    private Tween? tween;

    public void Build()
    {
    }

    public void Dispose() => tween?.Kill();

    internal void Show(string text, Color colour, double hold, double fadeIn, float top, int titleSize = TitleFontSize)
    {
        EnsureBanner();
        title.OffsetTop = top;
        subtitle.OffsetTop = top + SubtitleDrop;
        int split = text.IndexOf(SubtitleSeparator);
        title.Text = split < 0 ? text : text[..split];
        subtitle.Text = split < 0 ? "" : text[(split + 1)..];
        subtitle.Visible = subtitle.Text.Length > 0;
        title.AddThemeFontSizeOverride("font_size", titleSize);
        title.AddThemeColorOverride("font_color", colour);
        subtitle.AddThemeColorOverride("font_color", colour);

        var shown = banner!;
        tween?.Kill();
        shown.Modulate = new Color(1f, 1f, 1f, 0f);
        shown.Visible = true;
        tween = context.Root.CreateTween();
        tween.TweenProperty(shown, "modulate:a", 1f, fadeIn);
        tween.TweenInterval(hold);
        tween.TweenProperty(shown, "modulate:a", 0f, FadeOut);
        tween.TweenCallback(Callable.From(() => shown.Visible = false));
    }

    private void EnsureBanner()
    {
        if (banner != null && GodotObject.IsInstanceValid(banner)) return;
        var layer = new CanvasLayer { Layer = UiLayers.Notices };
        context.Root.AddChild(layer);
        banner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        banner.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        layer.AddChild(banner);
        title = Line(TitleFontSize);
        subtitle = Line(SubtitleFontSize);
        banner.AddChild(title);
        banner.AddChild(subtitle);
    }

    private static Label Line(int fontSize)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }
}
