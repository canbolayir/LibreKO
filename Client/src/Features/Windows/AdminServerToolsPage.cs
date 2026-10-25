using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminServerToolsPage
{
    private const int BotsMax = 2000;
    private const int BotsDefault = 50;
    private const int LevelMax = 83;
    private const float ToolWidth = 130;

    private readonly IChatRequests chat;
    private readonly Action<string, string, Action> confirm;

    public AdminServerToolsPage(IChatRequests chat, Action<string, string, Action> confirm)
    {
        this.chat = chat;
        this.confirm = confirm;
    }

    internal Control Build()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);

        var online = Section(body, "Who is online", "system/users-three");
        AddAction(online, "Online count", "How many players are online", () => chat.Send("+online"), ToolWidth);
        AddAction(online, "Busiest zones", "The ten zones with the most players", () => chat.Send("+countzone"), ToolWidth);
        AddAction(online, "By level", "Players by level band", () => chat.Send("+countlevel"), ToolWidth);

        var data = Section(body, "Game data", "system/scroll");
        AddAction(data, "Reload quest scripts", "Reload the quest scripts without a restart",
            () => confirm("Reload every quest script now?", "Reload", () => chat.Send("+reloadscripts")), ToolWidth);
        AddAction(data, "Daily ranks now", "Recompute the daily rankings now",
            () => confirm("Recompute the daily rankings now?", "Recompute", () => chat.Send("+dailyrank")), ToolWidth);
        var reseed = AddAction(data, "Reseed from files", "Write the JSON data into the database and reload it",
            () => confirm("Reseed the database from the JSON files and reload? Changes made here and not in the files are lost.", "Reseed",
                () => chat.Send("+reseed")), ToolWidth);
        reseed.AddThemeColorOverride("font_color", UiTheme.Bad);

        var bots = Section(body, "Bots", "system/user");
        var count = Spin(1, BotsMax, 90);
        count.Value = BotsDefault;
        count.TooltipText = "How many bots in total";
        bots.AddChild(count);
        bots.AddChild(UiTheme.Text("levels", 13, UiTheme.TextHi));
        LevelRange(bots, LevelMax, 70, out var minLevel, out var maxLevel);
        minLevel.Value = 1;
        maxLevel.Value = LevelMax;
        AddAction(bots, "Spawn bots", "Bots appear around you",
            () => confirm($"Run {(int)count.Value} bots around you?", "Spawn", () => chat.Send($"+bots {(int)count.Value} {(int)minLevel.Value} {(int)maxLevel.Value}")),
            ToolWidth);
        AddAction(bots, "Status", "How many bots run", () => chat.Send("+bots status"), ToolWidth);
        AddAction(bots, "Stop bots", "Remove every bot", () => confirm("Stop every bot?", "Stop", () => chat.Send("+bots stop")), ToolWidth);

        var merchants = Section(body, "Merchant bots", "system/storefront");
        AddAction(merchants, "Load", "Load and spawn the saved merchant bots", () => chat.Send("+loadbotmerchant"), ToolWidth);
        AddAction(merchants, "Save", "Save the merchant bots that run now", () => chat.Send("+savemerchantbots"), ToolWidth);
        AddAction(merchants, "Clear", "Remove every merchant bot",
            () => confirm("Remove every merchant bot?", "Clear", () => chat.Send("+clearmerchantbots")), ToolWidth);
        return body;
    }
}
