using Godot;

namespace LibreKO.Features.Windows;

internal static class AdminPanelUi
{
    internal const float LabelWidth = 58;
    internal const float SpinWidth = 110;
    internal const float ColumnGap = 32;
    internal const float ControlHeight = 30;
    internal const float TabWidth = 640;
    internal const float ActionWidth = 110;
    internal const float RowButtonWidth = 72;
    internal const int StartsShown = 3;
    private const float SearchWidth = 100;
    private const int RowPadding = 8;
    private const int SectionFontSize = 14;
    private const int SmallFontSize = 12;

    internal static HBoxContainer Heading(string text, string icon, int size = 15)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 7);
        row.AddChild(UiIcons.Image(icon, new Vector2(size + 3, size + 3), UiTheme.GoldVivid));
        var label = UiTheme.Text(text, size, UiTheme.GoldVivid);
        label.AddThemeFontOverride("font", UiTheme.Strong);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        return row;
    }

    internal static Label FieldLabel(string text, float width = LabelWidth)
    {
        var label = UiTheme.Text(text, 15, UiTheme.TextHi);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    internal static SpinBox Spin(int min, int max, float width) =>
        UiTheme.NumberBox(min, max, 1, width, 14);

    internal static Button Button(string text, float minWidth = 0)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(minWidth, ControlHeight),
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        return button;
    }

    internal static void FocusLater(Control control) =>
        Callable.From(() => { if (GodotObject.IsInstanceValid(control) && control.IsVisibleInTree()) control.GrabFocus(); }).CallDeferred();

    internal static Control HeaderRow(HBoxContainer line)
    {
        var holder = new MarginContainer();
        holder.AddThemeConstantOverride("margin_left", RowPadding);
        holder.AddThemeConstantOverride("margin_right", RowPadding);
        holder.AddChild(line);
        return holder;
    }

    internal static Label HeaderCell(string text, float width, bool expand = false)
    {
        var label = UiTheme.Text(text, 11, UiTheme.TextDim);
        label.CustomMinimumSize = new Vector2(width, 0);
        if (expand) label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    internal static HFlowContainer Section(Container body, string title, string icon)
    {
        body.AddChild(Heading(title, icon, SectionFontSize));
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        body.AddChild(flow);
        return flow;
    }

    internal static Button AddAction(Container parent, string text, string tooltip, Action pressed, float width = ActionWidth)
    {
        var button = Button(text, width);
        button.TooltipText = tooltip;
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    internal static Button RowButton(string text, string tooltip, float width = RowButtonWidth)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(width, 0),
            TooltipText = tooltip,
        };
        button.AddThemeFontSizeOverride("font_size", SmallFontSize);
        return button;
    }

    internal static Button PickButton(float width, string tooltip)
    {
        var button = new Button
        {
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.None,
            ClipText = true,
            CustomMinimumSize = new Vector2(width, 0),
            TooltipText = tooltip,
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_hover_color", UiTheme.GoldBright);
        return button;
    }

    internal static (HBoxContainer Bar, LineEdit Query, Label Count) SearchBar(string placeholder, Action search)
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 6);
        var query = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClearButtonEnabled = true,
        };
        query.TextSubmitted += _ => search();
        bar.AddChild(query);
        var button = Button("Search", SearchWidth);
        button.Pressed += search;
        bar.AddChild(button);
        var count = UiTheme.Text("", SmallFontSize, UiTheme.TextLo);
        count.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(count);
        return (bar, query, count);
    }

    internal static PanelContainer ListRow(out HBoxContainer line, bool selected = false)
    {
        var row = UiTheme.RowPanel(selected);
        line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 8);
        row.AddChild(line);
        return row;
    }

    internal static PanelContainer NamedRow(string name, int id, out HBoxContainer line)
    {
        var row = ListRow(out line);
        line.AddChild(RowTitle(name));
        line.AddChild(IdTag(id));
        return row;
    }

    internal static TextureRect Icon(Texture2D? texture, float size) => new()
    {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        MouseFilter = Control.MouseFilterEnum.Stop,
    };

    internal static TextureRect ItemIcon(int itemId, float size, IItemTooltips tooltips)
    {
        var icon = Icon(ItemIcons.Get(itemId), size);
        tooltips.Hover(icon, itemId);
        return icon;
    }

    internal static Label ItemName(int itemId, int fontSize)
    {
        var name = UiTheme.Text(ItemData.DisplayName(itemId), fontSize, ItemGrade.Tint(itemId));
        name.VerticalAlignment = VerticalAlignment.Center;
        name.ClipText = true;
        return name;
    }

    internal static Label RowTitle(string text)
    {
        var name = UiTheme.Text(text, 14, UiTheme.GoldBright);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.ClipText = true;
        return name;
    }

    internal static Label IdTag(int id)
    {
        var tag = UiTheme.Text($"#{id}", 10, UiTheme.TextDim);
        tag.VerticalAlignment = VerticalAlignment.Center;
        return tag;
    }

    internal static void TimeFields(Container parent, float width, out SpinBox hour, out SpinBox minute)
    {
        hour = Spin(0, 23, width);
        minute = Spin(0, 59, width);
        parent.AddChild(hour);
        parent.AddChild(UiTheme.Text(":", 15, UiTheme.TextHi));
        parent.AddChild(minute);
    }

    internal static void LevelRange(Container parent, int max, float width, out SpinBox low, out SpinBox high)
    {
        low = Spin(1, max, width);
        high = Spin(1, max, width);
        parent.AddChild(low);
        parent.AddChild(UiTheme.Text("to", 13, UiTheme.TextHi));
        parent.AddChild(high);
    }

    internal static bool IsOpen(HudWindow? window) => window != null && GodotObject.IsInstanceValid(window) && window.Visible;

    internal static void Hide(HudWindow? window)
    {
        if (window != null && GodotObject.IsInstanceValid(window)) window.Visible = false;
    }

    internal static Color KindColor(EventKind kind) => kind switch
    {
        EventKind.Temple => new Color("e2735f"),
        EventKind.CollectionRace => new Color("e3c15b"),
        EventKind.Lottery => new Color("7ccf7c"),
        EventKind.Krowaz => new Color("b18ce6"),
        EventKind.Bifrost => new Color("6cc7e0"),
        EventKind.CastleSiege => new Color("e89a4a"),
        EventKind.Bowl => new Color("d9d27a"),
        EventKind.NationWar => new Color("e05c8a"),
        EventKind.SnowWar => new Color("cfe8ff"),
        EventKind.Wanted => new Color("c9a56b"),
        _ => UiTheme.TextHi,
    };

    internal static void AddStartStop(HBoxContainer line, EventOwner owner, IChatRequests chat, Action<string, string, Action> confirm,
        string stopText = "Stop", string? stopQuestion = null)
    {
        if (GmCommands.StartEvent(owner) is { } start)
        {
            var startNow = UiTheme.SmallButton("Start now", "Start it now with its GM command");
            startNow.Pressed += () => confirm($"Start {owner.Name} now?", "Start", () => chat.Send(start));
            line.AddChild(startNow);
        }
        if (GmCommands.StopEvent(owner) is { } stop)
        {
            var stopNow = UiTheme.SmallButton(stopText, "End it now with its GM command");
            stopNow.Pressed += () => confirm(stopQuestion ?? $"{stopText} {owner.Name} now?", stopText, () => chat.Send(stop));
            line.AddChild(stopNow);
        }
    }
}
