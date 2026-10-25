using Godot;

namespace LibreKO.Features.Core;

internal sealed class RainScreenFeature : IFeature, ITickFeature
{
    private const float FadePerSecond = 0.35f;
    private const float MinRain = 0.3f;
    private const float MaxRain = 0.65f;
    private static readonly StringName Strength = "strength";
    private static readonly StringName Aspect = "aspect";

    private readonly IWorldContext context;
    private readonly ScenerySystem scenery;
    private CanvasLayer layer = null!;
    private ColorRect drops = null!;
    private ShaderMaterial material = null!;
    private float strength;
    private Vector2 lastSize;

    public RainScreenFeature(IWorldContext context, ScenerySystem scenery)
    {
        this.context = context;
        this.scenery = scenery;
    }

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.RainScreen, Visible = false };
        context.Root.AddChild(layer);
        material = Shaders.Material("rain_screen");
        drops = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
        drops.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(drops);
    }

    public void Dispose()
    {
        if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
    }

    public void Tick(double delta, double nowSec)
    {
        float rain = scenery.Sky?.RainOnScreen ?? 0f;
        float target = Config.RainEffects && rain > 0f ? Mathf.Lerp(MinRain, MaxRain, rain) : 0f;
        strength = Mathf.MoveToward(strength, target, (float)delta * FadePerSecond);
        bool shown = strength > 0f;
        if (layer.Visible != shown) layer.Visible = shown;
        if (!shown) return;
        material.SetShaderParameter(Strength, strength);
        var size = drops.GetViewportRect().Size;
        if (size == lastSize || size.Y <= 0f) return;
        lastSize = size;
        material.SetShaderParameter(Aspect, size.X / size.Y);
    }
}
