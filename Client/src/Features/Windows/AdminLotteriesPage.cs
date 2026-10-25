using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminLotteriesPage
{
    private readonly IChatRequests chat;
    private readonly Action<EventOwner> edit;
    private readonly Action create;
    private readonly Action<string, string, Action> confirm;

    public AdminLotteriesPage(IChatRequests chat, Action<EventOwner> edit, Action create, Action<string, string, Action> confirm)
    {
        this.chat = chat;
        this.edit = edit;
        this.create = create;
        this.confirm = confirm;
    }

    private VBoxContainer list = null!;

    internal Control Build()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 8);
        root.AddChild(head);
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var add = Button("New lottery", 130);
        add.Pressed += create;
        head.AddChild(add);
        list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 3);
        root.AddChild(list);
        return root;
    }

    internal void OnSchedules(AdminEventSchedules schedules)
    {
        if (list == null) return;
        UiKit.ClearChildren(list);
        var lotteries = schedules.Owners.Where(owner => owner.Kind == EventKind.Lottery).ToList();
        if (lotteries.Count == 0) list.AddChild(UiTheme.Text("No lotteries yet.", 13, UiTheme.TextLo));
        foreach (var lottery in lotteries) list.AddChild(Row(lottery));
    }

    private Control Row(EventOwner lottery)
    {
        var row = ListRow(out var line);
        line.AddChild(UiIcons.Image("system/ticket", new Vector2(18, 18), KindColor(EventKind.Lottery)));
        var name = UiTheme.Text(lottery.Name, 14, UiTheme.GoldBright);
        name.CustomMinimumSize = new Vector2(260, 0);
        name.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(name);
        var starts = UiTheme.Text(EventCalendar.Summary(lottery.Rules, StartsShown), 12, UiTheme.TextLo);
        starts.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        starts.VerticalAlignment = VerticalAlignment.Center;
        starts.ClipText = true;
        starts.TooltipText = string.Join("\n", lottery.Rules.Select(EventCalendar.Describe));
        starts.MouseFilter = Control.MouseFilterEnum.Pass;
        line.AddChild(starts);
        var editButton = UiTheme.SmallButton("Edit", "Its settings and rewards");
        editButton.Pressed += () => edit(lottery);
        line.AddChild(editButton);
        AddStartStop(line, lottery, chat, confirm, "Close", $"Close {lottery.Name} now and pay out?");
        return row;
    }
}
