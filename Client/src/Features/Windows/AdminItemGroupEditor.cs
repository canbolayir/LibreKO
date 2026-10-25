using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminItemGroupEditor
{
    private static readonly Vector2 ListSize = new(520, 340);
    private const float IconSize = 30;

    private readonly IAdminRequests requests;
    private readonly IItemTooltips tooltips;
    private readonly AdminItemPicker picker;
    private readonly Action<string, string, Action> confirm;

    public AdminItemGroupEditor(IAdminRequests requests, IItemTooltips tooltips, AdminItemPicker picker, Action<string, string, Action> confirm)
    {
        this.requests = requests;
        this.tooltips = tooltips;
        this.picker = picker;
        this.confirm = confirm;
    }

    internal HudWindow Window { get; private set; } = null!;
    private Label title = null!;
    private Label size = null!;
    private VBoxContainer list = null!;
    private Button apply = null!;
    private Button save = null!;
    private Button discard = null!;
    private int group;
    private int expected;
    private int[] original = [];
    private readonly List<int> items = new();

    internal bool Shown { get; private set; }

    internal void Build(Node layer)
    {
        Window = new HudWindow("item-group", "Item group", new Vector2(640, 160), titleIcon: UiIcons.Get("system/package")) { Visible = false };
        Window.Closed += Close;
        layer.AddChild(Window);

        var root = Window.Body;
        root.AddThemeConstantOverride("separation", 8);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        root.AddChild(head);
        title = UiTheme.Text("", 16, UiTheme.GoldVivid);
        title.AddThemeFontOverride("font", UiTheme.Strong);
        head.AddChild(title);
        size = UiTheme.Text("", 12, UiTheme.TextLo);
        size.VerticalAlignment = VerticalAlignment.Center;
        size.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(size);
        var add = Button("Add items", 120);
        add.Pressed += AddItems;
        head.AddChild(add);

        var scroll = new ScrollContainer { CustomMinimumSize = ListSize, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(list);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        apply = Button("Apply", 100);
        apply.TooltipText = "Change the group on this server now";
        apply.Pressed += () => requests.ItemGroupEdit(false, group, items.ToList());
        actions.AddChild(apply);
        save = Button("Apply and save", 150);
        save.TooltipText = "Change the group and save it in the database";
        save.Pressed += () => confirm($"Save item group {group} in the database?", "Save", () => requests.ItemGroupEdit(true, group, items.ToList()));
        actions.AddChild(save);
        discard = Button("Discard", 100);
        discard.Pressed += () =>
        {
            items.Clear();
            items.AddRange(original);
            Refresh();
        };
        actions.AddChild(discard);
    }

    internal void Request(int wanted)
    {
        expected = wanted;
        requests.ItemGroupRequest(wanted);
    }

    internal void Show(AdminItemGroup shown)
    {
        if (shown.Group != expected && !(Shown && shown.Group == group)) return;
        expected = 0;
        group = shown.Group;
        original = shown.Items;
        items.Clear();
        items.AddRange(shown.Items);
        title.Text = shown.Exists ? $"Item group {shown.Group}" : $"Item group {shown.Group} · new";
        Refresh();
        Shown = true;
        UiKit.ShowOnTop(Window);
    }

    private void Refresh()
    {
        UiKit.ClearChildren(list);
        size.Text = $"{items.Count} items, each as likely";
        for (int i = 0; i < items.Count; i++) list.AddChild(Row(i, items[i]));
        bool changed = !items.SequenceEqual(original);
        apply.Disabled = !changed || items.Count == 0;
        save.Disabled = apply.Disabled;
        discard.Disabled = !changed;
    }

    private Control Row(int index, int itemId)
    {
        var row = ListRow(out var line, !original.Contains(itemId));
        line.AddChild(ItemIcon(itemId, IconSize, tooltips));
        var name = ItemName(itemId, 13);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        line.AddChild(name);
        line.AddChild(IdTag(itemId));
        var remove = RowButton("Remove", "Take it out of the group; Discard brings it back", 0);
        remove.AddThemeColorOverride("font_color", UiTheme.Bad);
        remove.Pressed += () =>
        {
            items.RemoveAt(index);
            Refresh();
        };
        line.AddChild(remove);
        return row;
    }

    private void AddItems() =>
        picker.Open($"Add to item group {group}", "Add", false, (itemId, _) =>
        {
            if (!items.Contains(itemId)) items.Add(itemId);
            Refresh();
            UiKit.ShowOnTop(Window);
        }, onFamily: family =>
        {
            foreach (int itemId in family)
                if (!items.Contains(itemId)) items.Add(itemId);
            Refresh();
            UiKit.ShowOnTop(Window);
        });

    internal void Close()
    {
        if (!Shown) return;
        Shown = false;
        tooltips.Hide();
        Window.Visible = false;
    }
}
