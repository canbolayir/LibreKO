using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminMonsterPicker
{
    private static readonly Vector2 ListSize = new(480, 360);
    private const int ResultCap = 60;
    private const float PickWidth = 64;

    private HudWindow window = null!;
    private LineEdit query = null!;
    private VBoxContainer list = null!;
    private Action<int>? chosen;

    internal bool Shown => IsOpen(window);

    internal void Build(CanvasLayer layer)
    {
        window = new HudWindow("admin_monster_picker", "Choose a monster", new Vector2(560, 120), titleIcon: UiIcons.Get("system/combat-attack"))
        {
            Visible = false,
        };
        window.Closed += Close;
        layer.AddChild(window);
        var body = window.Body;
        body.AddThemeConstantOverride("separation", 8);
        query = new LineEdit { PlaceholderText = "Monster name or id", ClearButtonEnabled = true };
        query.TextChanged += _ => Search();
        body.AddChild(query);
        var scroll = new ScrollContainer { CustomMinimumSize = ListSize, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(list);
    }

    internal void Open(string title, Action<int> onChosen)
    {
        chosen = onChosen;
        window.Title = title;
        UiKit.ShowOnTop(window);
        Search();
        FocusLater(query);
    }

    private void Search()
    {
        UiKit.ClearChildren(list);
        string text = query.Text.Trim();
        if (text.Length == 0) return;
        foreach (var (id, name) in NpcNames.Search(text, monsters: true, ResultCap))
        {
            var row = NamedRow(name, id, out var line);
            var pick = RowButton("Pick", $"Use {name}", PickWidth);
            pick.Pressed += () => Choose(id);
            line.AddChild(pick);
            list.AddChild(row);
        }
    }

    private void Choose(int monster)
    {
        var then = chosen;
        Close();
        then?.Invoke(monster);
    }

    internal void Close()
    {
        chosen = null;
        Hide(window);
    }
}
