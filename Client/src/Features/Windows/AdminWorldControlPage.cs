using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminWorldControlPage
{
    private const int StrengthMax = 100;
    private const int StrengthDefault = 60;
    private const int BonusMax = 255;
    private const int MinutesMax = 1440;
    private const int SummonMax = 50;
    private const float NoticeWidth = 520;

    private static readonly (string Label, string Kind)[] Weathers =
    [
        ("Clear", "clear"), ("Rain", "rain"), ("Snow", "snow"),
    ];

    private static readonly (string Label, string Command)[] Bonuses =
    [
        ("Experience", "+exp_add"), ("Coins", "+money_add"), ("National points", "+np_add"), ("Drops", "+drop_add"),
    ];

    private const int MonsterResults = 30;

    private readonly IChatRequests chat;
    private readonly Action<string, string, Action> confirm;

    public AdminWorldControlPage(IChatRequests chat, Action<string, string, Action> confirm)
    {
        this.chat = chat;
        this.confirm = confirm;
    }

    private LineEdit monsterQuery = null!;
    private VBoxContainer monsterList = null!;

    internal Control Build()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);

        var time = Section(body, "Time and weather", "system/clock");
        TimeFields(time, 70, out var hour, out var minute);
        hour.Value = DateTime.Now.Hour;
        AddAction(time, "Set time", "Set the game time and the event clock for everyone",
            () => chat.Send($"+time {(int)hour.Value:00}:{(int)minute.Value:00}"));
        AddAction(time, "Real time", "Go back to the real clock", () => chat.Send("+time reset"));
        var weather = UiTheme.Dropdown(Weathers.Select(entry => entry.Label).ToArray());
        weather.CustomMinimumSize = new Vector2(170, ControlHeight);
        time.AddChild(weather);
        var strength = Spin(0, StrengthMax, 70);
        strength.Value = StrengthDefault;
        strength.TooltipText = "Strength, 0 to 100";
        time.AddChild(strength);
        AddAction(time, "Set weather", "Set the weather for everyone",
            () => chat.Send($"+weather {Weathers[Math.Max(0, weather.Selected)].Kind} {(int)strength.Value}"));

        var notice = Section(body, "Announcement", "system/chat-circle");
        var text = new LineEdit { PlaceholderText = "What everyone reads", CustomMinimumSize = new Vector2(NoticeWidth, ControlHeight) };
        notice.AddChild(text);
        AddAction(notice, "Announce", "Send a server notice to everyone", () =>
        {
            string message = text.Text.Trim();
            if (message.Length == 0) return;
            confirm($"Announce to everyone: \"{message}\"?", "Announce", () =>
            {
                chat.Send($"+notice {message}");
                text.Text = "";
            });
        });

        var bonus = Section(body, "Event bonuses", "system/sparkle");
        var kind = UiTheme.Dropdown(Bonuses.Select(entry => entry.Label).ToArray());
        kind.CustomMinimumSize = new Vector2(170, ControlHeight);
        bonus.AddChild(kind);
        var percent = Spin(0, BonusMax, 76);
        percent.Value = 50;
        bonus.AddChild(percent);
        bonus.AddChild(UiTheme.Text("% for", 13, UiTheme.TextHi));
        var minutes = Spin(0, MinutesMax, 80);
        minutes.Value = 60;
        minutes.TooltipText = "0 runs until you end it";
        bonus.AddChild(minutes);
        bonus.AddChild(UiTheme.Text("minutes", 13, UiTheme.TextHi));
        AddAction(bonus, "Start", "Start the bonus for everyone", () =>
        {
            var (label, command) = Bonuses[Math.Max(0, kind.Selected)];
            string line = minutes.Value > 0 ? $"{command} {(int)percent.Value} {(int)minutes.Value}" : $"{command} {(int)percent.Value}";
            confirm($"Give everyone +{(int)percent.Value}% {label.ToLowerInvariant()}?", "Start", () => chat.Send(line));
        });
        AddAction(bonus, "End", "End this bonus", () =>
        {
            var (label, command) = Bonuses[Math.Max(0, kind.Selected)];
            confirm($"End the {label.ToLowerInvariant()} bonus?", "End", () => chat.Send($"{command} 0"));
        });

        body.AddChild(Heading("Monsters", "system/combat-attack", 14));
        var searchRow = new HBoxContainer();
        searchRow.AddThemeConstantOverride("separation", 6);
        body.AddChild(searchRow);
        monsterQuery = new LineEdit { PlaceholderText = "Monster name or id", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClearButtonEnabled = true };
        monsterQuery.TextChanged += _ => SearchMonsters();
        searchRow.AddChild(monsterQuery);
        var killAll = Button("Kill all in my zone", 170);
        killAll.AddThemeColorOverride("font_color", UiTheme.Bad);
        killAll.Pressed += () => confirm("Kill every NPC in your zone?", "Kill all", () => chat.Send("+killall"));
        searchRow.AddChild(killAll);
        monsterList = new VBoxContainer();
        monsterList.AddThemeConstantOverride("separation", 3);
        body.AddChild(monsterList);

        var rebuild = Section(body, "Zone NPCs", "system/refresh");
        var zone = UiTheme.Dropdown(ZoneCatalog.All.Select(entry => entry.Name).ToArray());
        zone.CustomMinimumSize = new Vector2(240, ControlHeight);
        rebuild.AddChild(zone);
        AddAction(rebuild, "Rebuild", "Respawn every NPC of the zone from its spawn rows", () =>
        {
            if (zone.Selected < 0 || zone.Selected >= ZoneCatalog.All.Count()) return;
            var chosen = ZoneCatalog.All.ElementAt(zone.Selected);
            confirm($"Respawn every NPC of {chosen.Name}?", "Rebuild", () => chat.Send($"+npcrebuild {chosen.Id}"));
        });
        return body;
    }

    private void SearchMonsters()
    {
        UiKit.ClearChildren(monsterList);
        string text = monsterQuery.Text.Trim();
        if (text.Length == 0) return;
        foreach (var (monsterId, monsterName) in NpcNames.Search(text, monsters: true, MonsterResults))
        {
            var row = NamedRow(monsterName, monsterId, out var line);
            var count = Spin(1, SummonMax, 70);
            line.AddChild(count);
            var once = UiTheme.SmallButton("Summon here", "They appear around you once and never respawn");
            once.Pressed += () => chat.Send($"+monsummon {monsterId} {(int)count.Value}");
            line.AddChild(once);
            var lasting = UiTheme.SmallButton("Spawn here", "They appear around you and respawn");
            lasting.Pressed += () => confirm($"Spawn {(int)count.Value} {monsterName} here for good? They respawn.", "Spawn",
                () => chat.Send($"+mon {monsterId} {(int)count.Value}"));
            line.AddChild(lasting);
            monsterList.AddChild(row);
        }
    }
}
