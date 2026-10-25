using Godot;

namespace LibreKO.Features.Windows;

internal sealed class AdminRacesTab
{
    private readonly IAdminRequests requests;
    private readonly Action<int> edit;
    private readonly Action create;

    public AdminRacesTab(IAdminRequests requests, Action<int> edit, Action create)
    {
        this.requests = requests;
        this.edit = edit;
        this.create = create;
    }

    private LineEdit raceFilter = null!;
    private VBoxContainer raceList = null!;
    private CheckBox raceActiveOnly = null!;
    private List<AdminCollectionRace> races = [];

    internal Control Build()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);

        var findRow = new HBoxContainer();
        findRow.AddThemeConstantOverride("separation", 5);
        box.AddChild(findRow);
        raceFilter = new LineEdit
        {
            PlaceholderText = "Filter by name, zone or level",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClearButtonEnabled = true,
        };
        raceFilter.TextChanged += _ => RefreshRaceList();
        findRow.AddChild(raceFilter);
        raceActiveOnly = UiTheme.FlatCheck("Active only", false);
        raceActiveOnly.Toggled += _ => RefreshRaceList();
        findRow.AddChild(raceActiveOnly);
        var refresh = UiTheme.IconButton(UiIcons.Get("system/refresh"), "Refresh");
        refresh.Pressed += () => requests.CollectionRacesRequest();
        findRow.AddChild(refresh);
        var newRace = AdminPanelUi.Button("New race", 110);
        newRace.Pressed += create;
        findRow.AddChild(newRace);

        raceList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        raceList.AddThemeConstantOverride("separation", 4);
        box.AddChild(raceList);
        return box;
    }

    internal void OnCollectionRaces(List<AdminCollectionRace> races)
    {
        this.races = races;
        RefreshRaceList();
    }

    private void RefreshRaceList()
    {
        if (raceList == null || !GodotObject.IsInstanceValid(raceList)) return;
        foreach (var child in raceList.GetChildren())
        {
            raceList.RemoveChild(child);
            child.QueueFree();
        }

        var filter = raceFilter.Text.Trim();
        var activeOnly = raceActiveOnly.ButtonPressed;
        foreach (var race in races)
        {
            if (activeOnly && !race.Active) continue;
            if (filter.Length > 0 && !RaceMatches(race, filter)) continue;
            raceList.AddChild(BuildRaceRow(race));
        }
    }

    private static bool RaceMatches(AdminCollectionRace race, string filter) =>
        race.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
    || ZoneCatalog.Name(race.ZoneId).Contains(filter, System.StringComparison.OrdinalIgnoreCase)
    || race.ZoneId.ToString() == filter
    || $"{race.MinLevel}-{race.MaxLevel}".Contains(filter, System.StringComparison.Ordinal);

    private Control BuildRaceRow(AdminCollectionRace race)
    {
        var panel = UiTheme.RowPanel(race.Active);
        var margin = new MarginContainer();
        UiTheme.Margins(margin, 8, 5, 8, 5);
        panel.AddChild(margin);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 2);
        margin.AddChild(rows);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 8);
        rows.AddChild(head);
        var title = UiTheme.Text($"{race.Id}  {race.Name}", 13, race.Active ? UiTheme.Good : UiTheme.GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(title);
        head.AddChild(UiTheme.Pill(race.Active ? $"running  {race.RemainingSeconds / 60:D2}:{race.RemainingSeconds % 60:D2}" : race.AutoStart ? "scheduled" : "manual",
            race.Active ? UiTheme.Good : UiTheme.TextLo));
        var raceId = race.Id;
        var editRace = UiTheme.SmallButton("Edit", "Its objectives, rewards and settings");
        editRace.Pressed += () => edit(raceId);
        head.AddChild(editRace);
        var action = UiTheme.SmallButton(race.Active ? "Close" : "Start", race.Active ? "End this race now; completers are mailed their rewards" : "Start this race now in its zone");
        var wasActive = race.Active;
        action.Pressed += () =>
        {
            if (wasActive) requests.CollectionRaceClose(raceId);
            else requests.CollectionRaceStart(raceId);
        };
        head.AddChild(action);

        var meta = UiTheme.Text(
            $"{ZoneCatalog.Name(race.ZoneId)} ({race.ZoneId})  ·  level {race.MinLevel}-{race.MaxLevel}  ·  {race.DurationMinutes} min" +
            (race.MaxWinners > 0 ? $"  ·  {race.MaxWinners} winners" : "  ·  no winner limit") +
            (race.Active ? $"  ·  {race.Completions} finished" : ""),
            12, UiTheme.TextLo);
        rows.AddChild(meta);
        var objectives = UiTheme.Text(race.Objectives, 12, UiTheme.TextHi);
        objectives.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rows.AddChild(objectives);
        var schedule = UiTheme.Text($"Schedule (UTC): {race.Schedule}", 11, UiTheme.TextDim);
        schedule.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rows.AddChild(schedule);
        return panel;
    }
}
