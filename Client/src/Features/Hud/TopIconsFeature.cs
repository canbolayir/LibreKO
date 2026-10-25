using Godot;

namespace LibreKO.Features.Hud;

internal enum TopIconSlot { AttendanceGift, Trophy, Mail, PowerUpStore, Lottery, TimedItems, Events, Genie, DailyRank, Siege, Craft, Discord }

internal sealed class TopIconsFeature : IFeature
{
    private const float IconGap = 8f;
    private const float TopRightTolerance = 48f;
    private const string DiscordInvite = "https://discord.gg/wyQ335WfPx";
    private static readonly Color DiscordBlurple = new("5865f2");

    private readonly IWorldContext context;
    private readonly Action openPowerUpStore;
    private readonly Action toggleLottery;
    private readonly Action toggleGenie;
    private readonly Action openCraft;
    private readonly Dictionary<TopIconSlot, Control> icons = new();
    private CanvasLayer layer = null!;
    private Control? anchor;
    private Rect2 row;
    private Button lotteryIcon = null!;
    private TextureRect lotteryImage = null!;
    private Button powerUpStoreIcon = null!;
    private TextureRect powerUpStoreImage = null!;
    private Button craftIcon = null!;

    public TopIconsFeature(IWorldContext context, Action openPowerUpStore, Action toggleLottery, Action toggleGenie, Action openCraft)
    {
        this.context = context;
        this.openPowerUpStore = openPowerUpStore;
        this.toggleLottery = toggleLottery;
        this.toggleGenie = toggleGenie;
        this.openCraft = openCraft;
    }

    internal CanvasLayer HudLayer => layer;

    internal Rect2 Row => row;

    internal Control? Icon(TopIconSlot slot) => icons.TryGetValue(slot, out var icon) ? icon : null;

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.HudButtons };
        context.Root.AddChild(layer);

        powerUpStoreIcon = Add(TopIconSlot.PowerUpStore, "system/gem", "Power-Up Store", openPowerUpStore, out powerUpStoreImage);
        powerUpStoreImage.SelfModulate = UiTheme.Premium;
        powerUpStoreIcon.Resized += () => Place(TopIconSlot.PowerUpStore, HudPlacement.PowerUpStoreIcon);

        lotteryIcon = Add(TopIconSlot.Lottery, "system/ticket", "Lottery Event", toggleLottery, out lotteryImage);
        lotteryImage.SelfModulate = UiKit.TopIconColor(true);
        lotteryIcon.Visible = false;
        lotteryIcon.Resized += () => Place(TopIconSlot.Lottery, HudPlacement.LotteryIcon);
        lotteryIcon.VisibilityChanged += () => Place(TopIconSlot.Lottery, HudPlacement.LotteryIcon);

        var genie = Add(TopIconSlot.Genie, "system/sparkle", "Genie", toggleGenie, out _);
        genie.Visible = !Platform.TouchUi;

        craftIcon = Add(TopIconSlot.Craft, "system/hammer", "Item Combination", openCraft, out var craftImage);
        craftImage.SelfModulate = UiKit.TopIconColor(true);
        craftIcon.Visible = false;
        craftIcon.VisibilityChanged += () => Place(TopIconSlot.Craft, HudPlacement.CraftIcon);

        var discord = Add(TopIconSlot.Discord, "brand/discord", "Join the LibreKO Discord", () => OS.ShellOpen(DiscordInvite), out var discordImage,
            disc: DiscordBlurple);
        discordImage.SelfModulate = Colors.White;
        discord.Visible = !Platform.TouchUi;

        if (context.Hud.PluginPart(HudPart.MiniMap) is { } themedMap)
        {
            themedMap.ItemRectChanged += PlaceAll;
            themedMap.VisibilityChanged += PlaceAll;
        }
        Callable.From(PlaceAll).CallDeferred();
        Callable.From(() => Place(TopIconSlot.PowerUpStore, HudPlacement.PowerUpStoreIcon)).CallDeferred();
        Callable.From(() => Place(TopIconSlot.Lottery, HudPlacement.LotteryIcon)).CallDeferred();
    }

    public void Dispose()
    {
        if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
    }

    internal void SetAnchor(Control chip)
    {
        anchor = chip;
        chip.Resized += PlaceAll;
    }

    private static readonly Dictionary<TopIconSlot, string> Captions = new()
    {
        [TopIconSlot.AttendanceGift] = "Daily", [TopIconSlot.Trophy] = "Awards", [TopIconSlot.Mail] = "Mail",
        [TopIconSlot.PowerUpStore] = "Store", [TopIconSlot.Lottery] = "Lottery", [TopIconSlot.TimedItems] = "Timed",
        [TopIconSlot.Events] = "Events", [TopIconSlot.Genie] = "Genie", [TopIconSlot.DailyRank] = "Ranks", [TopIconSlot.Siege] = "Siege",
        [TopIconSlot.Craft] = "Craft", [TopIconSlot.Discord] = "Discord",
    };

    internal Button Add(TopIconSlot slot, string iconId, string tooltip, Action pressed, out TextureRect image, CanvasLayer? layer = null,
        Color? disc = null)
    {
        var button = HudDisc.IconButton(iconId, tooltip, pressed, UiKit.TopIconColor(false), out image, disc: disc);
        (layer ?? this.layer).AddChild(button);
        HudCaption.Attach(button, Captions.GetValueOrDefault(slot, ""));
        icons[slot] = button;
        return button;
    }

    internal void RefreshLottery(bool active)
    {
        if (!GodotObject.IsInstanceValid(lotteryIcon)) return;
        lotteryIcon.Visible = active;
    }

    internal void RefreshCraft(bool shown)
    {
        if (!GodotObject.IsInstanceValid(craftIcon)) return;
        craftIcon.Visible = shown;
    }

    internal float MiniMapRightSpan()
    {
        var themed = context.Hud.PluginPart(HudPart.MiniMap);
        if (themed == null || !GodotObject.IsInstanceValid(themed))
            return HudAnchor.Edge + MiniMap.SquareSize;
        if (!themed.IsVisibleInTree() || themed.Size.X <= 0f) return HudAnchor.Edge;
        var rect = themed.GetGlobalRect();
        float screen = context.Root.GetViewport().GetVisibleRect().Size.X;
        bool topRight = rect.End.X >= screen - TopRightTolerance && rect.Position.Y <= TopRightTolerance;
        return topRight ? Mathf.Max(HudAnchor.Edge, screen - rect.Position.X) : HudAnchor.Edge;
    }

    internal void PinAnchor()
    {
        if (anchor == null || !GodotObject.IsInstanceValid(anchor)) return;
        HudAnchor.Pin(anchor, HudAnchor.Spot.TopRight, new Vector2(MiniMapRightSpan() + UiKit.StatusHudGap, HudAnchor.Edge));
    }

    internal void Place(TopIconSlot slot, HudPlacement.Slot touchSlot)
    {
        if (!Platform.TouchUi)
        {
            PlaceAll();
            return;
        }
        if (!icons.TryGetValue(slot, out var icon) || !GodotObject.IsInstanceValid(icon) || icon is not Button button) return;
        float side = HudPlacement.LauncherButtonSize;
        button.CustomMinimumSize = new Vector2(side, side);
        float inset = side * HudPlacement.LauncherGlyphInset;
        foreach (var child in button.GetChildren())
        {
            if (child is not TextureRect image) continue;
            image.OffsetLeft = image.OffsetTop = inset;
            image.OffsetRight = image.OffsetBottom = -inset;
        }
        touchSlot.ApplyTo(button);
    }

    internal void PlaceAll()
    {
        if (Platform.TouchUi || anchor == null || !GodotObject.IsInstanceValid(anchor)) return;

        PinAnchor();
        float side = HudPlacement.HudLauncherButtonSize;
        var size = new Vector2(side, side);
        float right = HudAnchor.Edge;
        float bottom = HudAnchor.Edge + (Config.IconCaptions ? HudCaption.Space : 0f);
        foreach (TopIconSlot slot in Enum.GetValues<TopIconSlot>())
        {
            if (!icons.TryGetValue(slot, out var icon) || !GodotObject.IsInstanceValid(icon) || !icon.Visible) continue;
            icon.CustomMinimumSize = size;
            HudAnchor.Pin(icon, HudAnchor.Spot.BottomRight, new Vector2(right, bottom), size);
            right += size.X + IconGap;
        }
        var row = anchor.Visible ? anchor.GetGlobalRect() : new Rect2();
        if (row.IsEqualApprox(this.row)) return;
        this.row = row;
        PluginHost.Game.RaiseTopRightRow();
    }
}
