using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminRaceEditor
{
    private const int MaxRows = 10;
    private const int IdMax = 999_999_999;
    private const int CountMax = 9_999;
    private const int MinutesMax = 1_440;
    private const int WinnersMax = 1_000;
    private const int LevelMax = 255;
    private const int ZoneMax = 255;
    private const int RateMax = 100;
    private const float IdWidth = 130;
    private const float NameWidth = 220;

    private readonly IAdminRequests requests;
    private readonly AdminItemPicker picker;
    private readonly AdminMonsterPicker monsterPicker;
    private readonly IItemTooltips tooltips;

    public AdminRaceEditor(IAdminRequests requests, AdminItemPicker picker, AdminMonsterPicker monsterPicker, IItemTooltips tooltips)
    {
        this.requests = requests;
        this.picker = picker;
        this.monsterPicker = monsterPicker;
        this.tooltips = tooltips;
    }

    internal HudWindow Window { get; private set; } = null!;
    private Label title = null!;
    private LineEdit name = null!;
    private SpinBox zone = null!;
    private Label zoneName = null!;
    private SpinBox minLevel = null!;
    private SpinBox maxLevel = null!;
    private SpinBox minutes = null!;
    private SpinBox winners = null!;
    private CheckBox autoStart = null!;
    private AdminRowList<TargetRow> targets = null!;
    private AdminRowList<RewardRow> rewards = null!;
    private Button delete = null!;

    internal int RaceId { get; private set; }

    internal bool Shown { get; private set; }

    private sealed record TargetRow(OptionButton Kind, SpinBox Target, AdminMonsterChooser Monster, AdminItemField Item, Label Enemy, SpinBox Count);

    private sealed record RewardRow(AdminItemField Item, SpinBox Count, SpinBox Rate);

    internal void Build(Node layer)
    {
        Window = new HudWindow("race-editor", "Collection Race", new Vector2(600, 90), 600) { Visible = false };
        Window.Closed += Close;
        layer.AddChild(Window);
        var root = Window.Body;
        root.AddThemeConstantOverride("separation", 8);

        title = UiTheme.Text("", 15, UiTheme.GoldBright);
        root.AddChild(title);
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        root.AddChild(grid);
        grid.AddChild(FieldLabel("Name", 90));
        name = new LineEdit { CustomMinimumSize = new Vector2(260, ControlHeight), MaxLength = 50 };
        grid.AddChild(name);
        grid.AddChild(FieldLabel("Zone", 90));
        var zoneRow = new HBoxContainer();
        zone = Spin(1, ZoneMax, 70);
        zone.ValueChanged += value => zoneName.Text = ZoneCatalog.Name((int)value);
        zoneRow.AddChild(zone);
        zoneName = UiTheme.Text("", 12, UiTheme.TextLo);
        zoneRow.AddChild(zoneName);
        grid.AddChild(zoneRow);
        grid.AddChild(FieldLabel("Levels", 90));
        var levels = new HBoxContainer();
        minLevel = Spin(1, LevelMax, 64);
        maxLevel = Spin(1, LevelMax, 64);
        levels.AddChild(minLevel);
        levels.AddChild(UiTheme.Text("to", 13, UiTheme.TextHi));
        levels.AddChild(maxLevel);
        grid.AddChild(levels);
        grid.AddChild(FieldLabel("Minutes", 90));
        minutes = Spin(1, MinutesMax, 90);
        grid.AddChild(minutes);
        grid.AddChild(FieldLabel("Winners", 90));
        winners = Spin(1, WinnersMax, 90);
        grid.AddChild(winners);
        autoStart = UiTheme.FlatCheck("Starts by itself", false, 14);
        grid.AddChild(autoStart);

        root.AddChild(UiTheme.Rule());
        root.AddChild(Heading("Targets", "system/combat-attack", 14));
        targets = new AdminRowList<TargetRow>(root, "Add target", MaxRows, line =>
        {
            var kind = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(130, ControlHeight) };
            kind.AddItem("Monster", (int)AdminRaceTarget.Monster);
            kind.AddItem("Item", (int)AdminRaceTarget.Item);
            kind.AddItem("Enemy players", (int)AdminRaceTarget.EnemyPlayer);
            line.AddChild(kind);
            var target = Spin(0, IdMax, IdWidth);
            target.Visible = false;
            line.AddChild(target);
            var monster = new AdminMonsterChooser(monsterPicker, "Monster to hunt", NameWidth);
            line.AddChild(monster);
            var item = new AdminItemField(picker, tooltips, "Item to collect", NameWidth);
            line.AddChild(item);
            var enemy = UiTheme.Text("players of the other nation", 13, UiTheme.TextHi);
            enemy.CustomMinimumSize = new Vector2(NameWidth, 0);
            enemy.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(enemy);
            line.AddChild(UiTheme.Text("×", 13, UiTheme.TextHi));
            var count = Spin(1, CountMax, 70);
            line.AddChild(count);
            var row = new TargetRow(kind, target, monster, item, enemy, count);
            item.Changed += itemId => row.Target.SetValueNoSignal(itemId);
            monster.Changed += monsterId => row.Target.SetValueNoSignal(monsterId);
            kind.ItemSelected += _ => RefreshTarget(row);
            RefreshTarget(row);
            return row;
        });

        root.AddChild(UiTheme.Rule());
        root.AddChild(Heading("Rewards", "system/gift", 14));
        rewards = new AdminRowList<RewardRow>(root, "Add reward", MaxRows, line =>
        {
            var item = new AdminItemField(picker, tooltips, "Race reward", NameWidth);
            line.AddChild(item);
            line.AddChild(UiTheme.Text("×", 13, UiTheme.TextHi));
            var count = Spin(1, CountMax, 70);
            line.AddChild(count);
            var rate = Spin(1, RateMax, 64);
            rate.Value = RateMax;
            rate.Suffix = "%";
            rate.TooltipText = "Chance a winner gets it";
            line.AddChild(rate);
            return new RewardRow(item, count, rate);
        });

        root.AddChild(UiTheme.Rule());
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        var save = Button("Save", 90);
        save.TooltipText = "Save the race in the database";
        save.Pressed += Save;
        actions.AddChild(save);
        delete = Button("Delete", 90);
        delete.TooltipText = "Delete the race with its targets, rewards and starts";
        delete.Pressed += Delete;
        actions.AddChild(delete);
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = Button("Close", 90);
        close.Pressed += Close;
        actions.AddChild(close);
    }

    private static void RefreshTarget(TargetRow row)
    {
        var kind = (AdminRaceTarget)row.Kind.GetItemId(row.Kind.Selected);
        if (kind == AdminRaceTarget.EnemyPlayer) row.Target.SetValueNoSignal(0);
        int id = (int)row.Target.Value;
        row.Monster.Visible = kind == AdminRaceTarget.Monster;
        row.Item.Visible = kind == AdminRaceTarget.Item;
        row.Enemy.Visible = kind == AdminRaceTarget.EnemyPlayer;
        if (kind == AdminRaceTarget.Monster && row.Monster.MonsterId != id) row.Monster.Set(id);
        if (kind == AdminRaceTarget.Item && row.Item.ItemId != id) row.Item.Set(id);
    }

    internal void Show(AdminRace shown)
    {
        RaceId = shown.Id;
        title.Text = shown.Id == 0 ? "New Collection Race" : $"{shown.Name}  ({shown.Id})" + (shown.Running ? "  ·  running now" : "");
        name.Text = shown.Name;
        zone.Value = shown.ZoneId;
        zoneName.Text = ZoneCatalog.Name(shown.ZoneId);
        minLevel.Value = shown.MinLevel;
        maxLevel.Value = shown.MaxLevel;
        minutes.Value = shown.DurationMinutes;
        winners.Value = shown.MaxWinners;
        autoStart.ButtonPressed = shown.AutoStart;
        targets.Clear();
        foreach (var objective in shown.Objectives)
        {
            var row = targets.Add();
            row.Kind.Select(row.Kind.GetItemIndex((int)objective.Kind));
            row.Target.SetValueNoSignal(objective.TargetId);
            row.Count.Value = objective.Count;
            RefreshTarget(row);
        }
        rewards.Clear();
        foreach (var reward in shown.Rewards)
        {
            var row = rewards.Add();
            row.Item.Set(reward.ItemId);
            row.Count.Value = reward.Count;
            row.Rate.Value = reward.Rate;
        }
        delete.Disabled = shown.Id == 0;
        Window.Visible = true;
        Shown = true;
        Window.GetParent()?.MoveChild(Window, Window.GetParent().GetChildCount() - 1);
        Callable.From(Window.ResetSize).CallDeferred();
    }

    private void Save()
    {
        var race = new AdminRace
        {
            Id = RaceId,
            Name = name.Text.Trim(),
            ZoneId = (int)zone.Value,
            MinLevel = (int)minLevel.Value,
            MaxLevel = (int)maxLevel.Value,
            DurationMinutes = (int)minutes.Value,
            MaxWinners = (int)winners.Value,
            AutoStart = autoStart.ButtonPressed,
        };
        foreach (var row in targets.Rows)
        {
            var kind = (AdminRaceTarget)row.Kind.GetItemId(row.Kind.Selected);
            race.Objectives.Add(new AdminRaceObjective(kind, kind == AdminRaceTarget.EnemyPlayer ? 0 : (int)row.Target.Value, (int)row.Count.Value, ""));
        }
        foreach (var row in rewards.Rows)
            if (row.Item.ItemId > 0) race.Rewards.Add(new AdminRaceReward(row.Item.ItemId, (int)row.Count.Value, (int)row.Rate.Value));
        requests.RaceSave(race);
    }

    private void Delete()
    {
        if (RaceId == 0) return;
        requests.RaceDelete(RaceId);
    }

    internal void Close()
    {
        if (!Shown) return;
        Shown = false;
        Window.Visible = false;
    }
}
