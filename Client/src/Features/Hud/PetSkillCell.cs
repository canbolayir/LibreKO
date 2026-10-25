using Godot;

namespace LibreKO.Features.Hud;

internal sealed partial class PetSkillCell : PanelContainer
{
    private const string SkillKey = "petSkill";
    private const string SlotKey = "petSlot";
    private const string BagKey = "invFrom";

    public int SkillId { get; private set; }
    public int Slot { get; set; } = -1;
    public bool Draggable { get; init; }
    public System.Action<int>? OnUse;
    public System.Action<int, int, int>? OnDrop;
    public System.Action<int>? OnClear;
    public System.Action<int>? OnRightClick;
    public System.Func<int, int>? BagItemAt;
    private readonly TextureRect icon;
    private readonly ColorRect cool;
    private readonly ShaderMaterial coolMat;
    private readonly TextureRect autoFrame;
    private int autoShown = -1;
    private static readonly Texture2D?[] AutoFrames = new Texture2D?[PetSkills.AutoFrameCount];
    private float coolShown = -1f;
    private bool dragging;
    private bool pressed;
    private static readonly StringName RemainParam = "remain";
    private static readonly Color DimColor = new(0.45f, 0.45f, 0.45f);

    public PetSkillCell(float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        AddThemeStyleboxOverride("panel", UiTheme.Slot());

        icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(icon);

        coolMat = new ShaderMaterial { Shader = Shaders.Get("cooldown") };
        cool = new ColorRect { Material = coolMat, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(cool);

        autoFrame = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        AddChild(autoFrame);
    }

    public void SetAutoFrame(int frame)
    {
        if (frame == autoShown) return;
        autoShown = frame;
        if (frame is < 0 or >= PetSkills.AutoFrameCount)
        {
            autoFrame.Visible = false;
            return;
        }
        AutoFrames[frame] ??= ResourceLoader.Load<Texture2D>(PetSkills.AutoFramePath(frame));
        autoFrame.Texture = AutoFrames[frame];
        autoFrame.Visible = autoFrame.Texture != null;
    }

    public void Set(int skillId, bool locked, bool dim, string tooltip)
    {
        SkillId = skillId;
        TooltipText = tooltip;
        icon.Texture = skillId == 0 ? null : locked ? SkillIcons.Enigma() : SkillIcons.Get(skillId);
        icon.Modulate = dim ? DimColor : Colors.White;
        AddThemeStyleboxOverride("panel", UiTheme.Slot(locked: locked));
    }

    public void SetItem(int itemId, string tooltip)
    {
        SkillId = itemId;
        TooltipText = tooltip;
        icon.Texture = ItemIcons.Get(itemId);
        icon.Modulate = Colors.White;
        AddThemeStyleboxOverride("panel", UiTheme.Slot());
    }

    public void SetCooldown(float frac)
    {
        bool on = frac > 0.001f;
        if (cool.Visible != on) cool.Visible = on;
        if (on && Mathf.Abs(frac - coolShown) > 0.002f)
        {
            coolMat.SetShaderParameter(RemainParam, frac);
            coolShown = frac;
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!Draggable || SkillId == 0 || Config.HotbarLocked) return default;
        pressed = false;
        dragging = OnClear != null;
        var preview = new PanelContainer { CustomMinimumSize = Size };
        preview.AddChild(new TextureRect
        {
            Texture = icon.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        DragLayer.Show(this, preview);
        return new Godot.Collections.Dictionary { { SkillKey, SkillId }, { SlotKey, OnClear != null ? Slot : -1 } };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (OnDrop == null || data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        return d.ContainsKey(SkillKey) || (BagItemAt != null && d.ContainsKey(BagKey));
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var d = data.AsGodotDictionary();
        if (d.ContainsKey(SkillKey)) OnDrop?.Invoke(Slot, d[SkillKey].AsInt32(), d[SlotKey].AsInt32());
        else if (BagItemAt != null && d.ContainsKey(BagKey)) OnDrop?.Invoke(Slot, BagItemAt(d[BagKey].AsInt32()), -1);
    }

    public override void _Notification(int what)
    {
        switch ((long)what)
        {
            case NotificationDragEnd:
                if (!dragging) return;
                dragging = false;
                if (!IsDragSuccessful()) OnClear?.Invoke(Slot);
                return;
            case NotificationMouseExit:
                pressed = false;
                return;
        }
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (SkillId != 0 && OnRightClick != null && ev is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            if (!right.Pressed) OnRightClick(Slot);
            AcceptEvent();
            return;
        }
        if (SkillId == 0 || OnUse == null || ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left } button) return;
        if (button.Pressed) pressed = true;
        else if (pressed)
        {
            pressed = false;
            OnUse(SkillId);
        }
        AcceptEvent();
    }
}
