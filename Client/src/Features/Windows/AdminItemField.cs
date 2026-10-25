using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed partial class AdminItemField : HBoxContainer
{
    private const float IconSize = 26;
    private const string EmptyText = "Choose an item";

    private readonly AdminItemPicker picker;
    private readonly string pickTitle;
    private readonly TextureRect icon;
    private readonly Button name;
    private readonly Label id;

    public int ItemId { get; private set; }

    public event Action<int>? Changed;

    public AdminItemField(AdminItemPicker picker, IItemTooltips tooltips, string pickTitle, float nameWidth)
    {
        this.picker = picker;
        this.pickTitle = pickTitle;
        AddThemeConstantOverride("separation", 6);
        icon = Icon(null, IconSize);
        icon.MouseEntered += () => { if (ItemId > 0) tooltips.Show(-1, UiKit.TooltipItem(ItemId)); };
        icon.MouseExited += tooltips.Hide;
        AddChild(icon);
        name = PickButton(nameWidth, "Choose another item");
        name.Pressed += Pick;
        AddChild(name);
        id = IdTag(0);
        AddChild(id);
        Set(0);
    }

    public void Set(int itemId)
    {
        ItemId = itemId;
        icon.Texture = itemId > 0 ? ItemIcons.Get(itemId) : UiIcons.Get("system/search");
        name.Text = itemId > 0 ? ItemData.DisplayName(itemId) : EmptyText;
        name.AddThemeColorOverride("font_color", itemId > 0 ? ItemGrade.Tint(itemId) : UiTheme.TextLo);
        id.Text = itemId > 0 ? $"#{itemId}" : "";
    }

    private void Pick() =>
        picker.Open(pickTitle, "Pick", false, (itemId, _) =>
        {
            Set(itemId);
            Changed?.Invoke(itemId);
        });
}
