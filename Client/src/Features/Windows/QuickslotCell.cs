using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class QuickslotCell : PanelContainer
{
    private const string SkillKey = "id";
    private const string CellKey = "quickslotCell";
    private const string HotbarKey = "barAbs";
    private const int NoCell = -1;

    private readonly TextureRect icon;
    private bool dragging;

    public int Cell { get; }
    public int SkillId { get; private set; }
    public System.Action<int, int, int>? OnDrop;
    public System.Action<int>? OnClear;

    public QuickslotCell(int cell, float size)
    {
        Cell = cell;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", UiTheme.Slot());
        icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(icon);
    }

    public void Set(int skillId)
    {
        SkillId = skillId;
        icon.Texture = skillId == 0 ? null : SkillIcons.Get(skillId);
        TooltipText = skillId == 0 ? "" : SkillData.Get(skillId)?.Name ?? "";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (SkillId == 0 || OnDrop == null) return default;
        dragging = true;
        var preview = new PanelContainer { CustomMinimumSize = Size };
        preview.AddChild(new TextureRect
        {
            Texture = icon.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        DragLayer.Show(this, preview);
        return new Godot.Collections.Dictionary { { SkillKey, SkillId }, { CellKey, Cell } };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (OnDrop == null || data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        return d.ContainsKey(SkillKey) && !d.ContainsKey(HotbarKey);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var d = data.AsGodotDictionary();
        OnDrop?.Invoke(Cell, d[SkillKey].AsInt32(), d.ContainsKey(CellKey) ? d[CellKey].AsInt32() : NoCell);
    }

    public override void _Notification(int what)
    {
        if ((long)what != NotificationDragEnd || !dragging) return;
        dragging = false;
        if (!IsDragSuccessful()) OnClear?.Invoke(Cell);
    }
}
