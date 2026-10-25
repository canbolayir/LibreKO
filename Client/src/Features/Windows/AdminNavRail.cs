using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class AdminNavRail : VBoxContainer
{
    internal const float Width = 210;
    private const int GroupFontSize = 14;
    private const int PageFontSize = 13;
    private const int PageIndent = 16;
    private const int IconSize = 16;
    private const float EntryHeight = 28;

    private readonly IReadOnlyList<GmPanelPage> pages;
    private readonly VBoxContainer list;
    private string current = "";
    private string openGroup = "";

    public event Action<string>? PageChosen;

    public AdminNavRail(IReadOnlyList<GmPanelPage> pages)
    {
        this.pages = pages;
        CustomMinimumSize = new Vector2(Width, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 6);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);
        list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(list);
    }

    internal void Select(string pageId)
    {
        current = pageId;
        if (GmPanelPages.Get(pageId) is { } page) openGroup = page.Group;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in list.GetChildren()) child.QueueFree();
        foreach (var group in GmPanelPages.Groups)
        {
            var inGroup = pages.Where(page => page.Group == group.Id).ToList();
            if (inGroup.Count == 0) continue;
            list.AddChild(GroupEntry(group, inGroup));
            if (group.Id != openGroup) continue;
            foreach (var page in inGroup) list.AddChild(PageEntry(page));
        }
    }

    private Button GroupEntry(GmPanelGroup group, List<GmPanelPage> inGroup)
    {
        bool open = group.Id == openGroup;
        var button = Entry(group.Title, group.Icon, GroupFontSize, 0, selected: false);
        button.AddThemeColorOverride("font_color", open ? UiTheme.GoldVivid : UiTheme.GoldBright);
        button.Pressed += () =>
        {
            if (open)
            {
                openGroup = "";
                Rebuild();
                return;
            }
            openGroup = group.Id;
            if (inGroup.All(page => page.Id != current)) PageChosen?.Invoke(inGroup[0].Id);
            else Rebuild();
        };
        return button;
    }

    private Button PageEntry(GmPanelPage page)
    {
        var button = Entry(page.Title, page.Icon, PageFontSize, PageIndent, page.Id == current);
        button.Pressed += () => PageChosen?.Invoke(page.Id);
        return button;
    }

    private static Button Entry(string text, string icon, int fontSize, int indent, bool selected)
    {
        var button = new Button
        {
            Text = text,
            Icon = UiIcons.Get(icon),
            ExpandIcon = false,
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, EntryHeight),
            ClipText = true,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeConstantOverride("icon_max_width", IconSize);
        button.AddThemeConstantOverride("h_separation", 8);
        button.AddThemeStyleboxOverride("normal", EntryStyle(selected, hover: false, indent));
        button.AddThemeStyleboxOverride("hover", EntryStyle(selected, hover: true, indent));
        button.AddThemeStyleboxOverride("pressed", EntryStyle(true, hover: false, indent));
        button.AddThemeStyleboxOverride("hover_pressed", EntryStyle(true, hover: true, indent));
        button.AddThemeColorOverride("font_color", selected ? UiTheme.GoldVivid : UiTheme.TextHi);
        button.AddThemeColorOverride("font_hover_color", UiTheme.GoldBright);
        button.AddThemeColorOverride("icon_normal_color", selected ? UiTheme.GoldVivid : UiTheme.TextLo);
        button.AddThemeColorOverride("icon_hover_color", UiTheme.GoldBright);
        return button;
    }

    private static StyleBoxFlat EntryStyle(bool selected, bool hover, int indent)
    {
        var style = UiTheme.Row(selected);
        if (!selected && !hover)
        {
            style.BgColor = Colors.Transparent;
            style.BorderColor = Colors.Transparent;
        }
        style.ContentMarginLeft = 8 + indent;
        style.ContentMarginRight = 6;
        style.ContentMarginTop = style.ContentMarginBottom = 3;
        return style;
    }
}
