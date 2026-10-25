using Godot;

namespace LibreKO.Features.Windows;

internal sealed class AdminZonesTab
{

    private readonly IAdminRequests requests;
    private readonly Func<int> zone;

    public AdminZonesTab(IAdminRequests requests, Func<int> zone)
    {
        this.requests = requests;
        this.zone = zone;
    }

    internal LineEdit ZoneFilter { get; private set; } = null!;
    private ScrollContainer zoneScroll = null!;
    private VBoxContainer zoneList = null!;
    private Control? zoneHereRow;

    internal Control Build()
    {
        var box = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 6);

        var findRow = new HBoxContainer();
        findRow.AddThemeConstantOverride("separation", 5);
        box.AddChild(findRow);
        ZoneFilter = new LineEdit
        {
            PlaceholderText = "Zone name or number",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        ZoneFilter.TextChanged += _ => RefreshZoneList();
        findRow.AddChild(ZoneFilter);
        var clear = UiTheme.IconButton(UiIcons.Get("system/close"), "Clear the filter");
        clear.Pressed += () => { ZoneFilter.Text = ""; RefreshZoneList(); };
        findRow.AddChild(clear);

        zoneScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(zoneScroll);
        zoneList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        zoneList.AddThemeConstantOverride("separation", 3);
        zoneScroll.AddChild(zoneList);

        return box;
    }

    internal void Refresh()
    {
        if (zoneList == null) return;
        RefreshZoneList();
        if (zoneHereRow is { } row)
            Callable.From(() => zoneScroll.EnsureControlVisible(row)).CallDeferred();
    }

    internal void RefreshZoneList()
    {
        if (zoneList == null) return;
        foreach (Node child in zoneList.GetChildren()) child.QueueFree();
        zoneHereRow = null;

        string filter = ZoneFilter.Text.Trim();
        foreach (var entry in ZoneCatalog.All)
        {
            if (!ZoneMatches(entry, filter)) continue;
            var row = BuildZoneRow(entry);
            if (entry.Id == zone()) zoneHereRow = row;
            zoneList.AddChild(row);
        }
    }

    private static bool ZoneMatches(ZoneCatalog.Zone entry, string filter)
    {
        if (filter.Length == 0) return true;
        return entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.Id.ToString().StartsWith(filter, StringComparison.Ordinal);
    }

    private Control BuildZoneRow(ZoneCatalog.Zone entry)
    {
        bool here = entry.Id == zone();
        var row = UiTheme.RowPanel(here);
        row.CustomMinimumSize = new Vector2(0, 32);

        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 8);
        row.AddChild(line);

        var name = UiTheme.Text(entry.Name, 13, here ? UiTheme.GoldBright : UiTheme.TextHi);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.ClipText = true;
        line.AddChild(name);

        var id = UiTheme.Pill(entry.Id.ToString(), here ? UiTheme.GoldBright : UiTheme.TextLo);
        id.CustomMinimumSize = new Vector2(44, 0);
        id.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        line.AddChild(id);

        var go = new Button
        {
            Text = here ? "Here" : "Go",
            Disabled = here,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(58, 0),
            TooltipText = here ? "" : $"Travel to {entry.Name}",
        };
        go.AddThemeFontSizeOverride("font_size", 12);
        int target = entry.Id;
        go.Pressed += () => requests.Zone(target);
        line.AddChild(go);

        return row;
    }
}
