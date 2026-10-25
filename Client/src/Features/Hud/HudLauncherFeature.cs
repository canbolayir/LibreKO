using Godot;

namespace LibreKO.Features.Hud;

internal sealed partial class HudLauncherFeature : IFeature
{
    private readonly IWorldContext context;
    private readonly StatusHudFeature statusHud;
    private readonly Action goToTown;
    private readonly Action toggleParty;
    private readonly Action toggleGenie;
    private readonly IClientSettingsRequests settingsRequests;

    public HudLauncherFeature(IWorldContext context, StatusHudFeature statusHud, Action goToTown, Action toggleParty, Action toggleGenie,
        IClientSettingsRequests settingsRequests)
    {
        this.context = context;
        this.statusHud = statusHud;
        this.goToTown = goToTown;
        this.toggleParty = toggleParty;
        this.toggleGenie = toggleGenie;
        this.settingsRequests = settingsRequests;
    }

    public void Dispose() => statusHud.LauncherAnchorChanged -= Place;

    private static float LauncherButtonSize => HudPlacement.HudLauncherButtonSize;

    private const float LauncherGap = 4f;

    private CanvasLayer layer = null!;

    internal HBoxContainer Root { get; private set; } = null!;

    internal Button? TownButton { get; private set; }

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.HudLauncher };
        context.Root.AddChild(layer);
        if (!Platform.TouchUi) context.Hud.PluginSeam(layer, HudPart.Launcher);

        Root = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        Root.AddThemeConstantOverride("separation", (int)LauncherGap);
        layer.AddChild(Root);

        if (Platform.TouchUi) BuildTownButton();
        else
            Root.AddChild(LauncherButton(
                "system/home", "Go to town", goToTown, town: true, caption: "Town"));
        Root.AddChild(LauncherButton(
            "game/helmet", "Character Info", () => context.MainWindows.Toggle("Character"), caption: "Info"));
        Root.AddChild(LauncherButton(
            "game/main-hand", "Skills", () => context.MainWindows.Toggle("Skills"), caption: "Skills"));
        Root.AddChild(LauncherButton(
            "system/bag", "Inventory", () => context.MainWindows.Toggle("Inventory"), caption: "Inventory"));
        Root.AddChild(LauncherButton(
            "system/users-three", "Party", toggleParty, caption: "Party"));
        if (Platform.TouchUi)
            Root.AddChild(LauncherButton(
                "system/sparkle", "Genie", toggleGenie));

        if (!Platform.TouchUi)
            Root.AddChild(LauncherButton(
                "system/help", "Quest Journal", () => context.MainWindows.Toggle("Quests"), caption: "Quests"));

        if (Platform.TouchUi)
        {
            Root.AddChild(LauncherButton(
                "system/scroll", "Quest Journal", () => context.MainWindows.Toggle("Quests")));
            Root.AddChild(LauncherMenuButton("Settings", () => SettingsPanel.Open(context.Root, settingsRequests)));
        }

        int slots = Root.GetChildCount();
        Root.CustomMinimumSize = new Vector2(
            LauncherButtonSize * slots + LauncherGap * (slots - 1), LauncherButtonSize);

        statusHud.LauncherAnchorChanged += Place;
        Place();
    }

    internal void Place()
    {
        if (Root == null || !GodotObject.IsInstanceValid(Root)) return;
        HudPlacement.Launcher(statusHud.LauncherAnchor).ApplyTo(Root);
    }

    private void BuildTownButton()
    {
        TownButton = LauncherButton("system/home", "Go to town",
                                     goToTown, town: true);
        layer.AddChild(TownButton);
        HudPlacement.TownButton.ApplyTo(TownButton);
    }

    private static Button LauncherButton(
        string iconId,
        string tooltip,
        Action action,
        bool town = false,
        string caption = "")
    {
        Color normalIcon = Platform.TouchUi
            ? new Color(1f, 1f, 1f, 0.96f)
            : town
                ? new Color(0.82f, 0.72f, 0.43f, 0.90f)
                : new Color(0.79f, 0.80f, 0.78f, 0.88f);
        var button = HudDisc.IconButton(iconId, tooltip, action, normalIcon, out _,
            town ? HudPlacement.LauncherGlyphInset : HudDisc.GlyphInset);
        HudCaption.Attach(button, caption);
        return button;
    }

    private Button LauncherMenuButton(string tooltip, Action action)
    {
        var button = new Button
        {
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(LauncherButtonSize, LauncherButtonSize),
        };
        var flat = new StyleBoxEmpty();
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            button.AddThemeStyleboxOverride(state, flat);

        if (Platform.PointerUi) HudDisc.Behind(button);

        var bars = new LauncherMenuGlyph { MouseFilter = Control.MouseFilterEnum.Ignore };
        bars.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(bars);
        button.Pressed += action;
        return button;
    }

    private sealed partial class LauncherMenuGlyph : Control
    {
        private static readonly Color Line = new(0.86f, 0.87f, 0.86f, 0.92f);

        public override void _Draw()
        {
            float w = Size.X * 0.52f;
            float x = (Size.X - w) * 0.5f;
            float thickness = Mathf.Max(2f, Size.Y * 0.075f);
            float gap = Size.Y * 0.155f;
            float y = Size.Y * 0.5f - gap;
            for (int i = 0; i < 3; i++)
                DrawLine(new Vector2(x, y + gap * i), new Vector2(x + w, y + gap * i),
                         Line, thickness, true);
        }
    }
}
