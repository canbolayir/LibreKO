using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed partial class AdminMonsterChooser : HBoxContainer
{
    private const string EmptyText = "Choose a monster";

    private readonly AdminMonsterPicker picker;
    private readonly string pickTitle;
    private readonly Button name;
    private readonly Label id;

    public int MonsterId { get; private set; }

    public event Action<int>? Changed;

    public AdminMonsterChooser(AdminMonsterPicker picker, string pickTitle, float nameWidth)
    {
        this.picker = picker;
        this.pickTitle = pickTitle;
        AddThemeConstantOverride("separation", 6);
        name = PickButton(nameWidth, "Choose another monster");
        name.Pressed += Pick;
        AddChild(name);
        id = IdTag(0);
        AddChild(id);
        Set(0);
    }

    public void Set(int monsterId)
    {
        MonsterId = monsterId;
        name.Text = monsterId > 0 ? NpcNames.Name(monsterId, true) : EmptyText;
        name.AddThemeColorOverride("font_color", monsterId > 0 ? UiTheme.GoldBright : UiTheme.TextLo);
        id.Text = monsterId > 0 ? $"#{monsterId}" : "";
    }

    private void Pick() =>
        picker.Open(pickTitle, monsterId =>
        {
            Set(monsterId);
            Changed?.Invoke(monsterId);
        });
}
