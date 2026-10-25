using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminGroupPicker
{
    private static readonly Vector2 ListSize = new(560, 380);
    private const float SampleIconSize = 24;
    private const float GroupNameWidth = 110;
    private const float UseWidth = 64;

    private readonly IAdminRequests requests;
    private readonly IItemTooltips tooltips;

    public AdminGroupPicker(IAdminRequests requests, IItemTooltips tooltips)
    {
        this.requests = requests;
        this.tooltips = tooltips;
    }

    private HudWindow window = null!;
    private LineEdit query = null!;
    private VBoxContainer list = null!;
    private Label count = null!;
    private Action<int>? chosen;

    internal bool Shown => IsOpen(window);

    internal void Build(CanvasLayer layer)
    {
        window = new HudWindow("admin_group_picker", "Choose an item group", new Vector2(540, 120), titleIcon: UiIcons.Get("system/package"))
        {
            Visible = false,
        };
        window.Closed += Close;
        layer.AddChild(window);
        var body = window.Body;
        body.AddThemeConstantOverride("separation", 8);

        var (bar, search, found) = SearchBar("Group number, or an item it holds", Search);
        query = search;
        count = found;
        body.AddChild(bar);

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

    private void Search() => requests.ItemGroupsRequest(query.Text.Trim());

    internal void OnGroups(AdminItemGroups groups)
    {
        if (!Shown) return;
        UiKit.ClearChildren(list);
        count.Text = GmPanelText.FoundCount(groups.Rows.Count, groups.Total);
        foreach (var row in groups.Rows) list.AddChild(Row(row));
    }

    private Control Row(AdminItemGroupRow group)
    {
        var row = ListRow(out var line);
        var name = UiTheme.Text($"Group {group.Group}", 14, UiTheme.GoldBright);
        name.CustomMinimumSize = new Vector2(GroupNameWidth, 0);
        name.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(name);
        var samples = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        samples.AddThemeConstantOverride("separation", 2);
        line.AddChild(samples);
        foreach (int item in group.Samples) samples.AddChild(ItemIcon(item, SampleIconSize, tooltips));
        var size = UiTheme.Text($"{group.ItemCount} items", 12, UiTheme.TextLo);
        size.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(size);
        var use = RowButton("Use", $"Use group {group.Group}", UseWidth);
        use.Pressed += () => Choose(group.Group);
        line.AddChild(use);
        return row;
    }

    private void Choose(int group)
    {
        var then = chosen;
        Close();
        then?.Invoke(group);
    }

    internal void Close()
    {
        chosen = null;
        tooltips.Hide();
        Hide(window);
    }
}
