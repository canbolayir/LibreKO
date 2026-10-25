using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminStartEditor
{
    private const int YearMin = 2000;
    private const int YearMax = 2100;
    private const int LevelMax = 255;
    private const int CountdownMax = 60;
    private const int DefaultMinLevel = 20;
    private const int DefaultMaxLevel = 83;
    private const int DefaultCountdown = 10;
    private const float LabelColumn = 90;

    private readonly IAdminRequests requests;
    private readonly Action<string, string, Action> confirm;

    public AdminStartEditor(IAdminRequests requests, Action<string, string, Action> confirm)
    {
        this.requests = requests;
        this.confirm = confirm;
    }

    private HudWindow window = null!;
    private OptionButton ownerPick = null!;
    private OptionButton repeatPick = null!;
    private HBoxContainer dateRow = null!;
    private SpinBox year = null!;
    private SpinBox month = null!;
    private SpinBox dayOfMonthDate = null!;
    private HBoxContainer weekdayRow = null!;
    private OptionButton weekdayPick = null!;
    private HBoxContainer monthlyRow = null!;
    private SpinBox dayOfMonth = null!;
    private SpinBox hour = null!;
    private SpinBox minute = null!;
    private Label clock = null!;
    private HBoxContainer levelRow = null!;
    private SpinBox minLevel = null!;
    private SpinBox maxLevel = null!;
    private HBoxContainer countdownRow = null!;
    private SpinBox countdown = null!;
    private Button save = null!;
    private Button delete = null!;
    private List<EventOwner> owners = new();
    private EventRule? rule;
    private string scheduleZone = "";

    internal bool Shown => IsOpen(window);

    internal HudWindow Window => window;

    internal void Build(CanvasLayer layer)
    {
        window = new HudWindow("admin_start_editor", "Event start", new Vector2(560, 160), titleIcon: UiIcons.Get("system/clock"))
        {
            Visible = false,
        };
        window.Closed += Close;
        layer.AddChild(window);
        var body = window.Body;
        body.AddThemeConstantOverride("separation", 8);

        var ownerRow = FieldRow(body, "Event");
        ownerPick = new OptionButton { CustomMinimumSize = new Vector2(300, ControlHeight), FocusMode = Control.FocusModeEnum.None };
        ownerPick.ItemSelected += _ => SyncOwnerFields();
        ownerRow.AddChild(ownerPick);

        var repeatRow = FieldRow(body, "Repeats");
        repeatPick = new OptionButton { CustomMinimumSize = new Vector2(160, ControlHeight), FocusMode = Control.FocusModeEnum.None };
        foreach (var repeat in new[] { ScheduleRepeat.Once, ScheduleRepeat.Daily, ScheduleRepeat.Weekly, ScheduleRepeat.Monthly })
            repeatPick.AddItem(EventCalendar.RepeatName(repeat), (int)repeat);
        repeatPick.ItemSelected += _ => SyncRepeatFields();
        repeatRow.AddChild(repeatPick);

        dateRow = FieldRow(body, "Date");
        year = Spin(YearMin, YearMax, 84);
        month = Spin(1, 12, 64);
        dayOfMonthDate = Spin(1, 31, 64);
        dateRow.AddChild(year);
        dateRow.AddChild(month);
        dateRow.AddChild(dayOfMonthDate);

        weekdayRow = FieldRow(body, "On");
        weekdayPick = new OptionButton { CustomMinimumSize = new Vector2(160, ControlHeight), FocusMode = Control.FocusModeEnum.None };
        foreach (var day in EventCalendar.WeekOrder) weekdayPick.AddItem(day.ToString(), (int)day);
        weekdayRow.AddChild(weekdayPick);

        monthlyRow = FieldRow(body, "Day");
        dayOfMonth = Spin(1, 31, 64);
        monthlyRow.AddChild(dayOfMonth);

        var timeRow = FieldRow(body, "At");
        TimeFields(timeRow, 64, out hour, out minute);
        clock = UiTheme.Text("", 12, UiTheme.TextLo);
        clock.VerticalAlignment = VerticalAlignment.Center;
        timeRow.AddChild(clock);

        levelRow = FieldRow(body, "Levels");
        LevelRange(levelRow, LevelMax, 70, out minLevel, out maxLevel);
        countdownRow = FieldRow(body, "Sign-up");
        countdown = Spin(1, CountdownMax, 70);
        countdownRow.AddChild(countdown);
        countdownRow.AddChild(UiTheme.Text("minutes", 13, UiTheme.TextHi));

        body.AddChild(UiTheme.Rule());
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        body.AddChild(actions);
        save = Button("Save", 100);
        save.TooltipText = "Change this start";
        save.Pressed += () => Save(asNew: false);
        actions.AddChild(save);
        var saveNew = Button("Save as new", 130);
        saveNew.TooltipText = "Add this as another start of the event";
        saveNew.Pressed += () => Save(asNew: true);
        actions.AddChild(saveNew);
        delete = Button("Delete", 110);
        delete.AddThemeColorOverride("font_color", UiTheme.Bad);
        delete.Pressed += Delete;
        actions.AddChild(delete);
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var cancel = Button("Cancel", 100);
        cancel.Pressed += Close;
        actions.AddChild(cancel);
    }

    private static HBoxContainer FieldRow(VBoxContainer body, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(FieldLabel(label, LabelColumn));
        body.AddChild(row);
        return row;
    }

    internal void Open(IReadOnlyList<EventOwner> editable, EventOwner owner, EventRule? startRule, DateOnly? day, string zone)
    {
        owners = editable.ToList();
        if (!owners.Contains(owner)) owners.Insert(0, owner);
        scheduleZone = zone;
        rule = startRule;
        ownerPick.Clear();
        foreach (var each in owners) ownerPick.AddItem(each.Name);
        ownerPick.Select(owners.IndexOf(owner));
        ownerPick.Disabled = startRule is { Id: > 0 };
        window.Title = startRule is { Id: > 0 } ? $"Start of {owner.Name}" : "New event start";

        var now = DateTime.Now;
        var date = startRule?.Date ?? day ?? DateOnly.FromDateTime(now);
        var repeat = startRule?.Repeat ?? (day != null ? ScheduleRepeat.Once : ScheduleRepeat.Daily);
        repeatPick.Select(repeatPick.GetItemIndex((int)repeat));
        year.Value = date.Year;
        month.Value = date.Month;
        dayOfMonthDate.Value = date.Day;
        weekdayPick.Select(weekdayPick.GetItemIndex((int)(startRule?.Day ?? date.DayOfWeek)));
        dayOfMonth.Value = startRule?.DayOfMonth is > 0 and var monthly ? monthly : date.Day;
        hour.Value = startRule?.Hour ?? now.Hour;
        minute.Value = startRule?.Minute ?? 0;
        minLevel.Value = startRule?.MinLevel is > 0 and var low ? low : DefaultMinLevel;
        maxLevel.Value = startRule?.MaxLevel is > 0 and var high ? high : DefaultMaxLevel;
        countdown.Value = startRule?.CountdownMinutes is > 0 and var count ? count : DefaultCountdown;
        save.Disabled = startRule is not { Id: > 0 };
        delete.Visible = startRule is { Id: > 0 };
        SyncRepeatFields();
        SyncOwnerFields();
        UiKit.ShowOnTop(window);
    }

    private EventOwner? SelectedOwner => ownerPick.Selected >= 0 && ownerPick.Selected < owners.Count ? owners[ownerPick.Selected] : null;

    private ScheduleRepeat SelectedRepeat => (ScheduleRepeat)repeatPick.GetItemId(repeatPick.Selected);

    private void SyncRepeatFields()
    {
        var repeat = SelectedRepeat;
        dateRow.Visible = repeat == ScheduleRepeat.Once;
        weekdayRow.Visible = repeat == ScheduleRepeat.Weekly;
        monthlyRow.Visible = repeat == ScheduleRepeat.Monthly;
        Callable.From(window.ResetSize).CallDeferred();
    }

    private void SyncOwnerFields()
    {
        var owner = SelectedOwner;
        levelRow.Visible = owner?.HasLevels == true;
        countdownRow.Visible = owner?.HasLevels == true;
        clock.Text = owner == null ? "" : owner.Clock == EventRuleClock.Utc ? "UTC" : $"{scheduleZone} time";
        Callable.From(window.ResetSize).CallDeferred();
    }

    private void Save(bool asNew)
    {
        if (SelectedOwner is not { Editable: true } owner) return;
        DateOnly? date = null;
        if (SelectedRepeat == ScheduleRepeat.Once)
        {
            int days = DateTime.DaysInMonth((int)year.Value, (int)month.Value);
            date = new DateOnly((int)year.Value, (int)month.Value, Math.Min((int)dayOfMonthDate.Value, days));
        }
        var edited = new EventRule(asNew ? 0 : rule?.Id ?? 0, SelectedRepeat, date, (DayOfWeek)weekdayPick.GetItemId(weekdayPick.Selected),
            (int)dayOfMonth.Value, (int)hour.Value, (int)minute.Value, (int)minLevel.Value, (int)maxLevel.Value, (int)countdown.Value);
        requests.EventRuleSave(owner.Kind, owner.Id, edited);
        Close();
    }

    private void Delete()
    {
        if (SelectedOwner is not { Editable: true } owner || rule is not { Id: > 0 } selected) return;
        confirm($"Delete this start of {owner.Name}: {EventCalendar.Describe(selected)}?", "Delete", () =>
        {
            requests.EventRuleDelete(owner.Kind, owner.Id, selected.Id);
            Close();
        });
    }

    internal void Close() => Hide(window);
}
