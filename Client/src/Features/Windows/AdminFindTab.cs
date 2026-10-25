using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminFindTab
{
    private const float FindLevelWidth = 48;
    private const float FindMapWidth = 150;
    private const float FindSpotWidth = 96;
    private const float FindButtonWidth = 86;
    private static readonly string[] FindKinds = ["NPC", "Monster"];

    private readonly IAdminRequests requests;
    private readonly Func<int> zone;

    public AdminFindTab(IAdminRequests requests, Func<int> zone)
    {
        this.requests = requests;
        this.zone = zone;
    }

    private OptionButton findKind = null!;
    internal LineEdit FindQuery { get; private set; } = null!;
    private Label findSummary = null!;
    private VBoxContainer findList = null!;
    private List<AdminFindHit> findHits = [];
    private int findTotal;

    internal Control Build()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);

        var (findRow, query, summary) = SearchBar("Name or id of a living monster or NPC", SendFind);
        FindQuery = query;
        findSummary = summary;
        box.AddChild(findRow);
        findKind = UiTheme.Dropdown(FindKinds);
        findKind.CustomMinimumSize = new Vector2(110, 0);
        findRow.AddChild(findKind);
        findRow.MoveChild(findKind, 0);

        box.AddChild(BuildFindHeader());
        findList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        findList.AddThemeConstantOverride("separation", 3);
        box.AddChild(findList);
        return box;
    }

    private static Control BuildFindHeader()
    {
        var line = new HBoxContainer();
        var holder = HeaderRow(line);
        line.AddThemeConstantOverride("separation", 8);
        line.AddChild(HeaderCell("Name", 0, expand: true));
        line.AddChild(HeaderCell("Level", FindLevelWidth));
        line.AddChild(HeaderCell("Map", FindMapWidth));
        line.AddChild(HeaderCell("Location", FindSpotWidth));
        line.AddChild(HeaderCell("", FindButtonWidth * 3 + 16));
        return holder;
    }

    private void SendFind()
    {
        string query = FindQuery.Text.Trim();
        if (query.Length == 0) return;
        ExpectFind(findKind.Selected);
        requests.Find(sentKind, query);
    }

    internal void ExpectFind(int kind) => sentKind = kind;

    private int sentKind = -1;

    internal void OnFind(int kind, int total, List<AdminFindHit> hits)
    {
        if (kind != sentKind) return;
        findHits = hits;
        findTotal = total;
        RefreshFindList();
    }

    private void RefreshFindList()
    {
        if (findList == null || !GodotObject.IsInstanceValid(findList)) return;
        UiKit.ClearChildren(findList);
        findSummary.Text = GmPanelText.FoundCount(findHits.Count, findTotal);

        foreach (var hit in findHits)
            findList.AddChild(BuildFindRow(hit));
    }

    private Control BuildFindRow(AdminFindHit hit)
    {
        var row = ListRow(out var line);
        row.CustomMinimumSize = new Vector2(0, 32);

        var nameCell = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        nameCell.AddThemeConstantOverride("separation", 6);
        line.AddChild(nameCell);
        var name = UiTheme.Text(hit.Name, 13, UiTheme.TextHi);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.ClipText = true;
        nameCell.AddChild(name);
        nameCell.AddChild(IdTag(hit.Id));
        if (hit.Bot)
        {
            var tag = UiTheme.Pill("bot", UiTheme.TextDim);
            tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            nameCell.AddChild(tag);
        }

        var level = UiTheme.Pill(hit.Level.ToString(), UiTheme.GoldBright);
        level.CustomMinimumSize = new Vector2(FindLevelWidth, 0);
        level.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        line.AddChild(level);

        var map = UiTheme.Text(ZoneCatalog.Name(hit.Zone), 12, hit.Zone == zone() ? UiTheme.GoldBright : UiTheme.TextHi);
        map.CustomMinimumSize = new Vector2(FindMapWidth, 0);
        map.ClipText = true;
        line.AddChild(map);

        var spot = UiTheme.Text($"{hit.X}, {hit.Z}", 12, UiTheme.TextHi);
        spot.CustomMinimumSize = new Vector2(FindSpotWidth, 0);
        line.AddChild(spot);

        var edit = RowButton("Edit", $"Open {hit.Name} on the Monster page: properties and drops", FindButtonWidth);
        edit.Pressed += () => requests.MonsterSheet(hit.Monster ? AdminMonsterBy.Monster : AdminMonsterBy.Npc, hit.Id);
        line.AddChild(edit);

        var spawn = RowButton("Spawn row", hit.SpawnRow > 0 ? $"Edit spawn row {hit.SpawnRow}" : "Not placed by a spawn row", FindButtonWidth);
        spawn.Disabled = hit.SpawnRow <= 0;
        spawn.Pressed += () => requests.SpawnRowRequest(hit.SpawnRow);
        line.AddChild(spawn);

        var go = RowButton("Go", $"Travel to {hit.Name} in {ZoneCatalog.Name(hit.Zone)}", FindButtonWidth);
        go.Pressed += () => requests.Go(hit.Zone, hit.X, hit.Z);
        line.AddChild(go);

        return row;
    }
}
