using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminLotteryEditor
{
    private const int MaxRows = 10;
    private const int CountMax = 9_999;
    private const int MinutesMax = 1_440;
    private const int UserLimitMax = 10_000;
    private const int CostMax = int.MaxValue;
    private const float NameWidth = 230;

    private readonly IAdminRequests requests;
    private readonly AdminItemPicker picker;
    private readonly IItemTooltips tooltips;

    public AdminLotteryEditor(IAdminRequests requests, AdminItemPicker picker, IItemTooltips tooltips)
    {
        this.requests = requests;
        this.picker = picker;
        this.tooltips = tooltips;
    }

    internal HudWindow Window { get; private set; } = null!;
    private Label title = null!;
    private LineEdit name = null!;
    private SpinBox minutes = null!;
    private SpinBox userLimit = null!;
    private AdminItemField ticketItem = null!;
    private SpinBox ticketCost = null!;
    private CheckBox autoStart = null!;
    private AdminRowList<RewardRow> rewards = null!;
    private Button delete = null!;
    private int lotteryId;

    internal bool Shown { get; private set; }

    internal int LotteryId => lotteryId;

    private sealed record RewardRow(SpinBox Place, AdminItemField Item, SpinBox Count);

    internal void Build(Node layer)
    {
        Window = new HudWindow("lottery-editor", "Lottery", new Vector2(620, 120), 560) { Visible = false };
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
        grid.AddChild(FieldLabel("Name", 110));
        name = new LineEdit { CustomMinimumSize = new Vector2(260, ControlHeight), MaxLength = 50 };
        grid.AddChild(name);
        grid.AddChild(new Control());
        grid.AddChild(new Control());
        grid.AddChild(FieldLabel("Minutes", 110));
        minutes = Spin(1, MinutesMax, 100);
        grid.AddChild(minutes);
        grid.AddChild(FieldLabel("Ticket limit", 110));
        userLimit = Spin(1, UserLimitMax, 100);
        grid.AddChild(userLimit);
        grid.AddChild(FieldLabel("Ticket item", 110));
        ticketItem = new AdminItemField(picker, tooltips, "Ticket item", NameWidth);
        grid.AddChild(ticketItem);
        grid.AddChild(FieldLabel("Ticket cost", 110));
        ticketCost = Spin(1, CostMax, 120);
        grid.AddChild(ticketCost);
        autoStart = UiTheme.FlatCheck("Starts by itself at its scheduled times", false, 14);
        root.AddChild(autoStart);

        root.AddChild(UiTheme.Rule());
        root.AddChild(Heading("Rewards", "system/gift", 14));
        rewards = new AdminRowList<RewardRow>(root, "Add reward", MaxRows, line =>
        {
            line.AddChild(UiTheme.Text("Place", 13, UiTheme.TextHi));
            var place = Spin(1, UserLimitMax, 70);
            line.AddChild(place);
            var item = new AdminItemField(picker, tooltips, "Lottery reward", NameWidth);
            line.AddChild(item);
            line.AddChild(UiTheme.Text("×", 13, UiTheme.TextHi));
            var count = Spin(1, CountMax, 70);
            line.AddChild(count);
            return new RewardRow(place, item, count);
        });

        root.AddChild(UiTheme.Rule());
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        var save = Button("Save", 90);
        save.TooltipText = "Save the lottery in the database";
        save.Pressed += Save;
        actions.AddChild(save);
        delete = Button("Delete", 90);
        delete.TooltipText = "Delete the lottery with its rewards and starts";
        delete.Pressed += Delete;
        actions.AddChild(delete);
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = Button("Close", 90);
        close.Pressed += Close;
        actions.AddChild(close);
    }

    internal void Show(AdminLottery shown)
    {
        lotteryId = shown.Id;
        title.Text = shown.Id == 0 ? "New lottery" : $"{shown.Name}  ({shown.Id})" + (shown.Running ? "  ·  drawing now" : "");
        name.Text = shown.Name;
        minutes.Value = shown.DurationMinutes;
        userLimit.Value = shown.UserLimit;
        ticketItem.Set(shown.ReqItemId);
        ticketCost.Value = shown.ReqItemCount;
        autoStart.ButtonPressed = shown.AutoStart;
        rewards.Clear();
        foreach (var reward in shown.Rewards)
        {
            var row = rewards.Add();
            row.Place.Value = reward.Place;
            row.Item.Set(reward.ItemId);
            row.Count.Value = reward.Count;
        }
        delete.Disabled = shown.Id == 0;
        Window.Visible = true;
        Shown = true;
        Window.GetParent()?.MoveChild(Window, Window.GetParent().GetChildCount() - 1);
        Callable.From(Window.ResetSize).CallDeferred();
    }

    private void Save()
    {
        var lottery = new AdminLottery
        {
            Id = lotteryId,
            Name = name.Text.Trim(),
            DurationMinutes = (int)minutes.Value,
            UserLimit = (int)userLimit.Value,
            ReqItemId = ticketItem.ItemId,
            ReqItemCount = (int)ticketCost.Value,
            AutoStart = autoStart.ButtonPressed,
        };
        foreach (var row in rewards.Rows)
            if (row.Item.ItemId > 0) lottery.Rewards.Add(new AdminLotteryReward((int)row.Place.Value, row.Item.ItemId, (int)row.Count.Value));
        requests.LotterySave(lottery);
    }

    private void Delete()
    {
        if (lotteryId == 0) return;
        requests.LotteryDelete(lotteryId);
    }

    internal void Close()
    {
        if (!Shown) return;
        Shown = false;
        Window.Visible = false;
    }
}
