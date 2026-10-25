using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class AdminTargetBar : PanelContainer
{
    private const float QueryWidth = 240;
    private const int SuggestionCap = 12;

    private readonly Label name;
    private readonly Label details;
    private readonly Label status;
    internal LineEdit Query { get; }
    private readonly PopupMenu suggestions;
    private readonly List<AdminFindHit> shown = new();

    public event Action<string>? SearchRequested;
    public event Action<AdminFindHit>? TargetPicked;
    public event Action? Cleared;

    public AdminTargetBar()
    {
        AddThemeStyleboxOverride("panel", UiTheme.Inset());
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);

        row.AddChild(UiIcons.Image("system/user", new Vector2(22, 22), UiTheme.GoldVivid));
        var who = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        who.AddThemeConstantOverride("separation", 0);
        row.AddChild(who);
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        who.AddChild(top);
        name = UiTheme.Text("", 16, UiTheme.GoldVivid);
        name.AddThemeFontOverride("font", UiTheme.Strong);
        top.AddChild(name);
        status = UiTheme.Text("", 12, UiTheme.TextLo);
        status.VerticalAlignment = VerticalAlignment.Center;
        top.AddChild(status);
        details = UiTheme.Text("", 12, UiTheme.TextLo);
        who.AddChild(details);

        Query = new LineEdit
        {
            PlaceholderText = "Player name or id",
            CustomMinimumSize = new Vector2(QueryWidth, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            ClearButtonEnabled = true,
        };
        Query.TextSubmitted += _ => Search();
        row.AddChild(Query);
        var find = UiTheme.IconButton(UiIcons.Get("system/search"), "Find the player");
        find.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        find.Pressed += Search;
        row.AddChild(find);
        var clear = UiTheme.IconButton(UiIcons.Get("system/close"), "Clear the target");
        clear.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        clear.Pressed += () => Cleared?.Invoke();
        row.AddChild(clear);

        suggestions = new PopupMenu();
        suggestions.IdPressed += id =>
        {
            if (id < 0 || id >= shown.Count) return;
            Query.Text = "";
            TargetPicked?.Invoke(shown[(int)id]);
        };
        AddChild(suggestions);
        ShowTarget(null);
    }

    private void Search()
    {
        string text = Query.Text.Trim();
        if (text.Length > 0) SearchRequested?.Invoke(text);
    }

    internal void ShowSuggestions(List<AdminFindHit> hits)
    {
        shown.Clear();
        suggestions.Clear();
        foreach (var hit in hits.Take(SuggestionCap))
        {
            suggestions.AddItem($"{hit.Name}   {GmPanelText.Presence(hit.Online, hit.LastSeen, DateTime.UtcNow)}", shown.Count);
            shown.Add(hit);
        }
        if (shown.Count == 0) suggestions.AddItem("Nobody matches", -1);
        var origin = Query.GetScreenPosition() + new Vector2(0, Query.Size.Y + 2);
        suggestions.Popup(new Rect2I((Vector2I)origin, new Vector2I((int)QueryWidth, 0)));
    }

    internal void ShowTarget(AdminPlayerSheet? sheet)
    {
        if (sheet == null)
        {
            name.Text = "No player chosen";
            name.AddThemeColorOverride("font_color", UiTheme.TextLo);
            status.Text = "";
            details.Text = "";
            return;
        }
        name.Text = sheet.Name;
        name.AddThemeColorOverride("font_color", UiTheme.GoldVivid);
        status.Text = GmPanelText.Presence(sheet.Online, sheet.LastSeen, DateTime.UtcNow) + (sheet.Online ? "" : " · view only");
        status.AddThemeColorOverride("font_color", sheet.Online ? UiTheme.Good : UiTheme.TextLo);
        details.Text = $"Level {sheet.Level} · {CharacterClassCatalog.SpecializationName(sheet.Class)} · " +
            $"{UiKit.NationName(sheet.Nation)} · {ZoneCatalog.Name(sheet.Zone)}";
    }
}
