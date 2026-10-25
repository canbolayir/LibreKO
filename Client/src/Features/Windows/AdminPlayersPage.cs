using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminPlayersPage
{
    private const float DotWidth = 14;
    private const float LevelWidth = 44;
    private const float ClassWidth = 132;
    private const float NationWidth = 76;
    private const float ZoneWidth = 130;
    private const float StatusWidth = 128;
    private const float RowHeight = 34;

    private readonly IAdminRequests requests;
    private readonly IChatRequests chat;
    private readonly Action<AdminFindHit> manage;

    public AdminPlayersPage(IAdminRequests requests, IChatRequests chat, Action<AdminFindHit> manage)
    {
        this.requests = requests;
        this.chat = chat;
        this.manage = manage;
    }

    internal LineEdit Query { get; private set; } = null!;
    private VBoxContainer list = null!;
    private Label count = null!;

    internal Control Build()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);

        var (bar, query, found) = SearchBar("Character name or id, online or not", Search);
        Query = query;
        count = found;
        box.AddChild(bar);

        box.AddChild(Header());
        list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        box.AddChild(list);
        return box;
    }

    private void Search()
    {
        string text = Query.Text.Trim();
        if (text.Length > 0) requests.Find((int)AdminFindKind.Players, text);
    }

    internal void OnFind(int total, List<AdminFindHit> hits)
    {
        UiKit.ClearChildren(list);
        count.Text = GmPanelText.FoundCount(hits.Count, total);
        foreach (var hit in hits) list.AddChild(Row(hit));
    }

    private static Control Header()
    {
        var line = new HBoxContainer();
        var row = HeaderRow(line);
        line.AddThemeConstantOverride("separation", 8);
        line.AddChild(HeaderCell("", DotWidth));
        line.AddChild(HeaderCell("Name", 0, expand: true));
        line.AddChild(HeaderCell("Level", LevelWidth));
        line.AddChild(HeaderCell("Class", ClassWidth));
        line.AddChild(HeaderCell("Nation", NationWidth));
        line.AddChild(HeaderCell("Zone", ZoneWidth));
        line.AddChild(HeaderCell("Status", StatusWidth));
        line.AddChild(HeaderCell("", RowButtonWidth * 3 + 16));
        return row;
    }

    private Control Row(AdminFindHit hit)
    {
        var row = ListRow(out var line);
        row.CustomMinimumSize = new Vector2(0, RowHeight);

        var dot = UiTheme.Text("●", 12, hit.Online ? UiTheme.Good : UiTheme.TextDim);
        dot.CustomMinimumSize = new Vector2(DotWidth, 0);
        dot.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(dot);

        var name = PickButton(0, $"Manage {hit.Name}  #{hit.Id}");
        name.Text = hit.Name;
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.AddThemeFontSizeOverride("font_size", 14);
        name.AddThemeColorOverride("font_color", UiTheme.GoldBright);
        name.AddThemeColorOverride("font_hover_color", UiTheme.GoldVivid);
        name.Pressed += () => manage(hit);
        line.AddChild(name);

        line.AddChild(Value(hit.Level.ToString(), LevelWidth, UiTheme.TextHi));
        line.AddChild(Value(CharacterClassCatalog.SpecializationName(hit.Class), ClassWidth, UiTheme.TextHi));
        line.AddChild(Value(UiKit.NationName(hit.Nation), NationWidth, UiKit.NationColor(hit.Nation)));
        line.AddChild(Value(ZoneCatalog.Name(hit.Zone), ZoneWidth, UiTheme.TextHi));
        line.AddChild(Value(GmPanelText.Presence(hit.Online, hit.LastSeen, DateTime.UtcNow), StatusWidth,
            hit.Online ? UiTheme.Good : UiTheme.TextLo));

        var manageButton = RowButton("Manage", $"Open {hit.Name} on the Player page");
        manageButton.Pressed += () => manage(hit);
        line.AddChild(manageButton);
        var go = RowButton("Go", $"Go to {hit.Name}");
        go.Disabled = !hit.Online;
        go.Pressed += () => chat.Send(GmCommands.ForPlayer(GmCommands.Goto, hit.Name));
        line.AddChild(go);
        var summon = RowButton("Summon", $"Bring {hit.Name} to you");
        summon.Disabled = !hit.Online;
        summon.Pressed += () => chat.Send(GmCommands.ForPlayer(GmCommands.SummonUser, hit.Name));
        line.AddChild(summon);
        return row;
    }

    private static Label Value(string text, float width, Color color)
    {
        var label = UiTheme.Text(text, 13, color);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true;
        return label;
    }
}
