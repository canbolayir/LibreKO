using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminEventsPage
{
    private const float CellWidth = 124;
    private const float CellHeight = 74;
    private const float WeekColumnHeight = 300;
    private const int ChipsPerCell = 3;
    private const int ChipFontSize = 11;
    private const int DurationMax = 1440;

    private readonly IAdminRequests requests;
    private readonly IChatRequests chat;
    private readonly AdminStartEditor startEditor;
    private readonly Action<string, string, Action> confirm;
    private readonly Action<EventOwner> editContent;

    public AdminEventsPage(IAdminRequests requests, IChatRequests chat, AdminStartEditor startEditor, Action<string, string, Action> confirm,
        Action<EventOwner> editContent)
    {
        this.requests = requests;
        this.chat = chat;
        this.startEditor = startEditor;
        this.confirm = confirm;
        this.editContent = editContent;
    }

    private AdminEventSchedules? schedules;
    private TimeZoneInfo? scheduleZone;
    private CalendarView view = CalendarView.Month;
    private DateOnly day = DateOnly.FromDateTime(DateTime.Now);
    private EventOwner? selected;
    private Label title = null!;
    private CheckBox onlySelected = null!;
    private VBoxContainer calendar = null!;
    private VBoxContainer eventList = null!;
    private readonly Dictionary<CalendarView, Button> viewButtons = new();

    internal bool Built { get; private set; }

    internal Control Build()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);

        var nav = new HBoxContainer();
        nav.AddThemeConstantOverride("separation", 8);
        root.AddChild(nav);
        var previous = Button("◀", 40);
        previous.Pressed += () => Move(-1);
        nav.AddChild(previous);
        title = UiTheme.Text("", 16, UiTheme.GoldBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nav.AddChild(title);
        var next = Button("▶", 40);
        next.Pressed += () => Move(1);
        nav.AddChild(next);
        var today = Button("Today", 80);
        today.Pressed += () => { day = DateOnly.FromDateTime(DateTime.Now); ShowCalendar(); };
        nav.AddChild(today);
        foreach (var (mode, label) in new[] { (CalendarView.Month, "Month"), (CalendarView.Week, "Week"), (CalendarView.Day, "Day") })
        {
            var button = UiTheme.UnderlineTabButton(label, 13);
            button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            button.CustomMinimumSize = new Vector2(70, ControlHeight);
            var chosen = mode;
            button.Pressed += () => SetView(chosen);
            viewButtons[mode] = button;
            nav.AddChild(button);
        }
        onlySelected = UiTheme.FlatCheck("Only the chosen event", false, 13);
        onlySelected.Toggled += _ => ShowCalendar();
        nav.AddChild(onlySelected);
        var refresh = UiTheme.IconButton(UiIcons.Get("system/refresh"), "Load the schedules again");
        refresh.Pressed += Request;
        nav.AddChild(refresh);

        calendar = new VBoxContainer();
        calendar.AddThemeConstantOverride("separation", 2);
        root.AddChild(calendar);

        root.AddChild(UiTheme.Rule());
        var listHead = new HBoxContainer();
        listHead.AddThemeConstantOverride("separation", 8);
        root.AddChild(listHead);
        var listTitle = Heading("Events", "system/clock", 14);
        listTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        listHead.AddChild(listTitle);
        var newStart = Button("New start", 120);
        newStart.TooltipText = "Add a start to the chosen event";
        newStart.Pressed += () => OpenEditor(selected ?? EditableOwners().FirstOrDefault(), null, null);
        listHead.AddChild(newStart);
        eventList = new VBoxContainer();
        eventList.AddThemeConstantOverride("separation", 3);
        root.AddChild(eventList);

        SetView(CalendarView.Month);
        Built = true;
        return root;
    }

    internal void Request() => requests.EventSchedulesRequest();

    internal void OnSchedules(AdminEventSchedules shown)
    {
        schedules = shown;
        scheduleZone = EventCalendar.FindZone(shown.ZoneId);
        selected = selected is { } previous ? shown.Owners.FirstOrDefault(o => o.Kind == previous.Kind && o.Id == previous.Id) : null;
        ShowCalendar();
        ShowEvents();
    }

    internal void SetView(CalendarView mode)
    {
        view = mode;
        foreach (var (key, button) in viewButtons) button.ButtonPressed = key == mode;
        ShowCalendar();
    }

    private void Move(int direction)
    {
        day = EventCalendar.Step(view, day, direction);
        ShowCalendar();
    }

    private List<EventStart> StartsBetween(DateOnly first, DateOnly last) => schedules == null
        ? new List<EventStart>()
        : EventCalendar.Starts(onlySelected.ButtonPressed && selected != null ? [selected] : schedules.Owners,
            first, last, scheduleZone, schedules.OffsetMinutes, TimeZoneInfo.Local);

    private void ShowCalendar()
    {
        if (calendar == null) return;
        title.Text = EventCalendar.Title(view, day);
        UiKit.ClearChildren(calendar);
        var (first, last) = EventCalendar.Range(view, day);
        var starts = StartsBetween(first, last);
        switch (view)
        {
            case CalendarView.Month: ShowMonth(first, starts); break;
            case CalendarView.Week: ShowWeek(first, starts); break;
            default: ShowDay(starts); break;
        }
    }

    private void ShowMonth(DateOnly first, List<EventStart> starts)
    {
        calendar.AddChild(WeekdayHeader());
        var grid = new GridContainer { Columns = EventCalendar.DaysPerWeek };
        grid.AddThemeConstantOverride("h_separation", 2);
        grid.AddThemeConstantOverride("v_separation", 2);
        calendar.AddChild(grid);
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (int i = 0; i < EventCalendar.GridDays; i++)
        {
            var date = first.AddDays(i);
            var cell = DayCell(date, date.Month == day.Month, date == today, CellHeight);
            var body = (VBoxContainer)cell.GetChild(0);
            var onDay = starts.Where(s => DateOnly.FromDateTime(s.Local) == date).ToList();
            int shown = onDay.Count > ChipsPerCell ? ChipsPerCell - 1 : onDay.Count;
            foreach (var start in onDay.Take(shown)) body.AddChild(Chip(start));
            if (onDay.Count > shown)
            {
                var more = UiTheme.Text($"+{onDay.Count - shown} more", ChipFontSize, UiTheme.TextLo);
                more.TooltipText = string.Join("\n", onDay.Select(s => $"{s.Local:HH:mm}  {s.Owner.Name}"));
                more.MouseFilter = Control.MouseFilterEnum.Pass;
                body.AddChild(more);
            }
            grid.AddChild(cell);
        }
    }

    private void ShowWeek(DateOnly first, List<EventStart> starts)
    {
        calendar.AddChild(WeekdayHeader(first));
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 2);
        calendar.AddChild(columns);
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (int i = 0; i < EventCalendar.DaysPerWeek; i++)
        {
            var date = first.AddDays(i);
            var cell = DayCell(date, true, date == today, WeekColumnHeight);
            var body = (VBoxContainer)cell.GetChild(0);
            foreach (var start in starts.Where(s => DateOnly.FromDateTime(s.Local) == date)) body.AddChild(Chip(start));
            columns.AddChild(cell);
        }
    }

    private void ShowDay(List<EventStart> starts)
    {
        if (starts.Count == 0)
        {
            calendar.AddChild(UiTheme.Text("Nothing starts on this day.", 13, UiTheme.TextLo));
            return;
        }
        foreach (var start in starts)
        {
            var row = ListRow(out var line, start.Owner == selected);
            var time = UiTheme.Text($"{start.Local:HH:mm}", 15, UiTheme.GoldBright);
            time.CustomMinimumSize = new Vector2(56, 0);
            line.AddChild(time);
            var name = UiTheme.Text(start.Owner.Name, 14, KindColor(start.Owner.Kind));
            name.CustomMinimumSize = new Vector2(260, 0);
            line.AddChild(name);
            var rule = UiTheme.Text(EventCalendar.Describe(start.Rule), 12, UiTheme.TextLo);
            rule.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            line.AddChild(rule);
            var edit = UiTheme.SmallButton("Edit", "Change this start");
            edit.Disabled = !start.Owner.Editable;
            var each = start;
            edit.Pressed += () => OpenEditor(each.Owner, each.Rule, null);
            line.AddChild(edit);
            calendar.AddChild(row);
        }
    }

    private static GridContainer WeekdayHeader(DateOnly? first = null)
    {
        var header = new GridContainer { Columns = EventCalendar.DaysPerWeek };
        header.AddThemeConstantOverride("h_separation", 2);
        for (int i = 0; i < EventCalendar.WeekOrder.Length; i++)
        {
            string weekday = EventCalendar.ShortName(EventCalendar.WeekOrder[i]);
            string text = first is { } start ? $"{weekday} {start.AddDays(i).Day}" : weekday;
            var label = UiTheme.Text(text, 12, UiTheme.TextLo);
            label.CustomMinimumSize = new Vector2(CellWidth, 0);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            header.AddChild(label);
        }
        return header;
    }

    private PanelContainer DayCell(DateOnly date, bool inMonth, bool today, float height)
    {
        var cell = new PanelContainer { CustomMinimumSize = new Vector2(CellWidth, height), MouseFilter = Control.MouseFilterEnum.Stop };
        var style = new StyleBoxFlat
        {
            BgColor = inMonth ? new Color(0.07f, 0.07f, 0.09f, 0.9f) : new Color(0.04f, 0.04f, 0.05f, 0.6f),
            BorderColor = today ? UiTheme.GoldVivid : new Color(1, 1, 1, 0.08f),
        };
        style.SetBorderWidthAll(today ? 2 : 1);
        style.SetCornerRadiusAll(3);
        style.ContentMarginLeft = style.ContentMarginRight = 4;
        cell.AddThemeStyleboxOverride("panel", style);
        cell.TooltipText = "Double-click to see the day; right-click to add a start";
        cell.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } click) return;
            if (click.ButtonIndex == MouseButton.Left && click.DoubleClick)
            {
                day = date;
                SetView(CalendarView.Day);
            }
            else if (click.ButtonIndex == MouseButton.Right) OpenEditor(selected ?? EditableOwners().FirstOrDefault(), null, date);
        };
        var body = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", 0);
        cell.AddChild(body);
        body.AddChild(UiTheme.Text(date.Day.ToString(), 12, inMonth ? UiTheme.TextHi : UiTheme.TextLo));
        return cell;
    }

    private Control Chip(EventStart start)
    {
        var chip = UiTheme.Text($"{start.Local:HH:mm} {start.Owner.Name}", ChipFontSize, KindColor(start.Owner.Kind));
        chip.ClipText = true;
        chip.CustomMinimumSize = new Vector2(CellWidth - 10, 0);
        chip.MouseFilter = Control.MouseFilterEnum.Stop;
        chip.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        chip.TooltipText = $"{start.Owner.Name}\n{EventCalendar.Describe(start.Rule)}";
        chip.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
            Select(start.Owner);
            OpenEditor(start.Owner, start.Rule, null);
            chip.AcceptEvent();
        };
        return chip;
    }

    private IReadOnlyList<EventOwner> EditableOwners() => schedules?.Owners.Where(owner => owner.Editable).ToList() ?? new List<EventOwner>();

    private void OpenEditor(EventOwner? owner, EventRule? rule, DateOnly? date)
    {
        if (owner == null || schedules == null) return;
        if (!owner.Editable && rule == null) return;
        startEditor.Open(EditableOwners(), owner, rule, date, schedules.ZoneId);
    }

    private void Select(EventOwner owner)
    {
        selected = owner;
        ShowEvents();
        if (onlySelected.ButtonPressed) ShowCalendar();
    }

    private void ShowEvents()
    {
        UiKit.ClearChildren(eventList);
        if (schedules == null) return;
        foreach (var owner in schedules.Owners) eventList.AddChild(EventRow(owner));
    }

    private Control EventRow(EventOwner owner)
    {
        var row = ListRow(out var line, owner == selected);
        row.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Select(owner);
        };
        var dot = UiTheme.Text("●", 12, KindColor(owner.Kind));
        dot.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(dot);
        var name = UiTheme.Text(owner.Name, 14, owner == selected ? UiTheme.GoldVivid : UiTheme.TextHi);
        name.CustomMinimumSize = new Vector2(220, 0);
        name.VerticalAlignment = VerticalAlignment.Center;
        name.ClipText = true;
        line.AddChild(name);
        var summary = UiTheme.Text(EventCalendar.Summary(owner.Rules, StartsShown), 12, UiTheme.TextLo);
        summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        summary.VerticalAlignment = VerticalAlignment.Center;
        summary.ClipText = true;
        summary.TooltipText = string.Join("\n", owner.Rules.Select(EventCalendar.Describe));
        summary.MouseFilter = Control.MouseFilterEnum.Pass;
        line.AddChild(summary);

        if (owner.DurationMinutes > 0)
        {
            var length = Spin(1, DurationMax, 76);
            length.Value = owner.DurationMinutes;
            length.TooltipText = "How long it runs each time, in minutes";
            line.AddChild(length);
            var setLength = UiTheme.SmallButton("Set length", "Save how long it runs");
            setLength.Pressed += () => requests.EventDurationSave(owner.Kind, owner.Id, (int)length.Value);
            line.AddChild(setLength);
        }
        if (owner.Kind is EventKind.Lottery or EventKind.CollectionRace)
        {
            var edit = UiTheme.SmallButton("Edit", "Its settings, rewards and targets");
            edit.Pressed += () => editContent(owner);
            line.AddChild(edit);
        }
        var add = UiTheme.SmallButton("New start", "Add a start to this event");
        add.Disabled = !owner.Editable;
        add.Pressed += () => { Select(owner); OpenEditor(owner, null, null); };
        line.AddChild(add);
        AddStartStop(line, owner, chat, confirm);
        return row;
    }
}
