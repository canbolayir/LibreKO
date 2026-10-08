using Godot;

namespace LibreKO;

public partial class FamiliarSkillButton : Button
{
    private readonly ColorRect _cooldown;
    private readonly ShaderMaterial _cooldownMaterial;
    private float _shown = -1;
    public int SkillId { get; internal set; }
    public float CooldownFraction { get; private set; }

    public FamiliarSkillButton()
    {
        ClipContents = true;
        _cooldownMaterial = Shaders.Material("cooldown");
        _cooldown = new ColorRect { Name = "pet_skill_cooldown", Material = _cooldownMaterial,
            MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(_cooldown); _cooldown.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _cooldown.OffsetLeft = _cooldown.OffsetTop = 2; _cooldown.OffsetRight = _cooldown.OffsetBottom = -2;
    }

    internal void SetCooldown(float fraction)
    {
        CooldownFraction = Mathf.Clamp(fraction, 0, 1);
        _cooldown.Visible = CooldownFraction > .001f;
        if (!_cooldown.Visible || Mathf.Abs(_shown - CooldownFraction) <= .002f) return;
        _cooldownMaterial.SetShaderParameter("remain", CooldownFraction); _shown = CooldownFraction;
    }
}
