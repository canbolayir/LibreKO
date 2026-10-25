using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminPlayerPage
{
    private const float AmountWidth = 140;
    private const float LoginWidth = 200;
    private const float StatWidth = 150;
    private const double RefreshDelaySeconds = 0.6;

    private static readonly (string Label, string Command)[] Progress =
    [
        ("Experience", "+exp_change"),
        ("National points", "+np_change"),
        ("Knight Cash", "+kc"),
        ("Slave Merchant hours", "+giveslave"),
    ];

    private readonly IChatRequests chat;
    private readonly Node timerHost;
    private readonly Action<string, string, Action> confirm;
    private readonly Action refresh;
    private readonly Action openInventory;

    public AdminPlayerPage(IChatRequests chat, Node timerHost, Action<string, string, Action> confirm, Action refresh, Action openInventory)
    {
        this.chat = chat;
        this.timerHost = timerHost;
        this.confirm = confirm;
        this.refresh = refresh;
        this.openInventory = openInventory;
    }

    private AdminPlayerSheet? target;
    private VBoxContainer body = null!;
    private Label gold = null!;
    private Label knightCash = null!;
    private Label loyalty = null!;
    private Label place = null!;
    private OptionButton progressKind = null!;
    private LineEdit amount = null!;
    private LineEdit login = null!;
    private readonly List<Button> onlineOnly = new();

    internal Control Build()
    {
        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 12);

        var stats = new HBoxContainer();
        stats.AddThemeConstantOverride("separation", 18);
        body.AddChild(stats);
        gold = Stat(stats, "Gold", "system/coins");
        knightCash = Stat(stats, "Knight Cash", "system/gem");
        loyalty = Stat(stats, "National points", "system/medal");
        place = Stat(stats, "Position", "system/pin");

        var travel = Section(body, "Travel", "system/door");
        onlineOnly.Add(AddAction(travel, "Go to", "Go to the player", () => Command(GmCommands.Goto)));
        onlineOnly.Add(AddAction(travel, "Summon", "Bring the player to you", () => Command(GmCommands.SummonUser)));

        var moderation = Section(body, "Moderation", "system/lock");
        onlineOnly.Add(AddAction(moderation, "Mute", "Stop the player's chat", () => Command("+mute")));
        onlineOnly.Add(AddAction(moderation, "Unmute", "Allow the player's chat", () => Command("+unmute")));
        onlineOnly.Add(AddAction(moderation, "Kick", "Disconnect the player", () => Ask($"Kick {Name} from the game?", "Kick", () => Command("+kick"))));
        onlineOnly.Add(AddAction(moderation, "Prison", "Send the player to prison", () => Ask($"Send {Name} to prison?", "Prison", () => Command("+hapis"))));
        onlineOnly.Add(AddAction(moderation, "Kill", "Kill the player", () => Ask($"Kill {Name}?", "Kill", () => Command("+kill"))));

        var account = Section(body, "Account", "system/key");
        AddAction(account, "Ban", "Ban the player's account", () => Ask($"Ban {Name}'s account?", "Ban", () => Command("+ban")));
        AddAction(account, "Force rename", "The player picks a new name at character selection",
            () => Ask($"Make {Name} choose a new name at character selection?", "Force rename", () => Command("+forcerename")));
        login = new LineEdit { PlaceholderText = "Account login", CustomMinimumSize = new Vector2(LoginWidth, ControlHeight) };
        account.AddChild(login);
        AddAction(account, "Unban", "Lift the ban of an account by its login", Unban);

        var progress = Section(body, "Progress", "system/level");
        progressKind = UiTheme.Dropdown(Progress.Select(entry => entry.Label).ToArray());
        progressKind.CustomMinimumSize = new Vector2(190, ControlHeight);
        progress.AddChild(progressKind);
        amount = new LineEdit { PlaceholderText = "Amount", CustomMinimumSize = new Vector2(AmountWidth, ControlHeight) };
        progress.AddChild(amount);
        onlineOnly.Add(AddAction(progress, "Give", "Add the amount", () => ChangeProgress(1)));
        onlineOnly.Add(AddAction(progress, "Take", "Take the amount away", () => ChangeProgress(-1)));

        var items = Section(body, "Items", "system/bag");
        AddAction(items, "Open inventory", "See and change the player's gear and bag", openInventory);
        onlineOnly.Add(AddAction(items, "Clear bag", "Empty the bag; worn gear stays",
            () => Ask($"Empty {Name}'s bag? Worn gear stays.", "Clear bag", () => Command("+clear"))));

        Show(null);
        return body;
    }

    private string Name => target?.Name ?? "";

    internal void Show(AdminPlayerSheet? sheet)
    {
        target = sheet;
        body.Modulate = sheet == null ? new Color(1, 1, 1, 0.45f) : Colors.White;
        foreach (var button in onlineOnly) button.Disabled = sheet is not { Online: true };
        gold.Text = sheet == null ? "-" : $"{sheet.Gold:n0}";
        knightCash.Text = sheet == null ? "-" : $"{sheet.KnightCash:n0}";
        loyalty.Text = sheet == null ? "-" : $"{sheet.Loyalty:n0}";
        place.Text = sheet == null ? "-" : $"{ZoneCatalog.Name(sheet.Zone)}  {sheet.X}, {sheet.Z}";
    }

    private void Command(string command)
    {
        if (target == null) return;
        chat.Send(GmCommands.ForPlayer(command, target.Name));
        timerHost.After(RefreshDelaySeconds, refresh);
    }

    private void Ask(string question, string confirmText, Action action)
    {
        if (target != null) confirm(question, confirmText, action);
    }

    private void Unban()
    {
        string account = login.Text.Trim();
        if (account.Length == 0) return;
        confirm($"Lift the ban of account {account}?", "Unban", () => chat.Send($"+unban {account}"));
    }

    private void ChangeProgress(int sign)
    {
        if (target == null || !GmPanelText.TryParseAmount(amount.Text, out long value) || value <= 0) return;
        var (label, command) = Progress[Math.Max(0, progressKind.Selected)];
        long signed = Math.Clamp(value * sign, int.MinValue, int.MaxValue);
        string verb = sign > 0 ? $"Give {Name} {value:n0} {label.ToLowerInvariant()}?" : $"Take {value:n0} {label.ToLowerInvariant()} from {Name}?";
        confirm(verb, sign > 0 ? "Give" : "Take", () =>
        {
            chat.Send($"{command} {target.Name} {signed}");
            timerHost.After(RefreshDelaySeconds, refresh);
        });
    }

    private static Label Stat(Container parent, string caption, string icon)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(StatWidth, 0) };
        card.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        parent.AddChild(card);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        card.AddChild(column);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 6);
        head.AddChild(UiIcons.Image(icon, new Vector2(14, 14), UiTheme.TextLo));
        head.AddChild(UiTheme.Text(caption, 11, UiTheme.TextLo));
        column.AddChild(head);
        var value = UiTheme.Text("-", 15, UiTheme.TextHi);
        column.AddChild(value);
        return value;
    }
}
