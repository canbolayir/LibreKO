using Godot;
using LibreKO.Features.Hud;
using LibreKO.Plugins;

namespace LibreKO.Features.Windows;

internal sealed class DrakiPlateFeature : IFeature, ITickFeature
{
    private readonly IWorldContext context;
    private readonly BifrostSession bifrost;
    private readonly EventPlatesFeature eventPlates;
    private readonly CaptionFeature caption;
    private readonly TownRecallFeature townRecall;
    private readonly DrakiBoardFeature board;

    public DrakiPlateFeature(IWorldContext context, BifrostSession bifrost, EventPlatesFeature eventPlates, CaptionFeature caption, TownRecallFeature townRecall, DrakiBoardFeature board)
    {
        this.context = context;
        this.bifrost = bifrost;
        this.eventPlates = eventPlates;
        this.caption = caption;
        this.townRecall = townRecall;
        this.board = board;
    }

    private const string LayoutId = "hud_draki_plate";
    private const string TownTip = "Right-click to go back to town";
    private const string SmallGlyph = "–";
    private const string LargeGlyph = "+";
    private const string SmallTip = "Smaller plate";
    private const string LargeTip = "Larger plate";
    private const int ChapterFontSize = 14;
    private const int LineFontSize = 13;
    private const int FloorFontSize = 30;
    private const int FloorNameFontSize = 26;
    private const int FloorOutline = 6;
    private const float FloorNameAt = 2f / 3f;
    private const float FloorLineAt = 0.5f;
    private const float ToggleSize = 18f;
    private static readonly Color FloorColour = new(1f, 0f, 0f);
    private static readonly Color FloorNameColour = new(1f, 1f, 0f);
    private static readonly Color LastSecondsColour = new(1f, 1f, 0f);

    private CanvasLayer plateLayer = null!;
    private CanvasLayer floorLayer = null!;
    private Label chapter = null!;
    private Label limit = null!;
    private Label elapsed = null!;
    private Button formToggle = null!;
    private Label floorLine = null!;
    private Label floorName = null!;
    private Tween? floorTween;
    private DrakiPlateLines shownLines;
    private bool large = true;
    private bool shown;

    internal PanelContainer Plate { get; private set; } = null!;

    internal Control FloorBand { get; private set; } = null!;

    public void Build()
    {
        plateLayer = new CanvasLayer { Layer = UiLayers.TopWindows };
        context.Root.AddChild(plateLayer);
        context.Hud.PluginSeam(plateLayer, HudPart.DrakiPlate);
        BuildPlate();
        floorLayer = new CanvasLayer { Layer = UiLayers.Notices };
        context.Root.AddChild(floorLayer);
        context.Hud.PluginSeam(floorLayer, HudPart.DrakiFloor);
        BuildFloor();
        bifrost.DrakiFloorEvent += OnFloor;
        bifrost.DrakiClosingEvent += OnClosing;
        PluginHost.Game.AttachDraki(new PluginDrakiBridge(this));
    }

    public void Dispose()
    {
        bifrost.DrakiFloorEvent -= OnFloor;
        bifrost.DrakiClosingEvent -= OnClosing;
        floorTween?.Kill();
    }

    private void BuildPlate()
    {
        Plate = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.Move,
            TooltipText = TownTip,
        };
        Plate.AddThemeStyleboxOverride("panel", UiTheme.Panel(5, true));
        Plate.GuiInput += OnPlateInput;
        plateLayer.AddChild(Plate);
        var layout = HudLayout.Attach(Plate, LayoutId, Plate, () => eventPlates.PlateSpot(Plate));
        eventPlates.AddPlate(Plate, layout, LayoutId);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        UiTheme.Margins(margin, 10, 4, 6, 6);
        Plate.AddChild(margin);
        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 2);
        margin.AddChild(rows);

        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 6);
        rows.AddChild(head);
        chapter = PlateLabel(ChapterFontSize, UiTheme.GoldBright);
        chapter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(chapter);
        formToggle = UiTheme.IconButton(SmallGlyph, SmallTip);
        formToggle.CustomMinimumSize = new Vector2(ToggleSize, ToggleSize);
        formToggle.AddThemeFontSizeOverride("font_size", LineFontSize);
        formToggle.Pressed += ToggleForm;
        head.AddChild(formToggle);

        limit = PlateLabel(LineFontSize, UiTheme.TextHi);
        rows.AddChild(limit);
        elapsed = PlateLabel(LineFontSize, UiTheme.TextHi);
        rows.AddChild(elapsed);
    }

    private static Label PlateLabel(int size, Color colour)
    {
        var label = UiTheme.Text("", size, colour, HorizontalAlignment.Center);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    private void BuildFloor()
    {
        FloorBand = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        FloorBand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        FloorBand.AnchorTop = 1f - DrakiTower.FloorBandShare;
        floorLayer.AddChild(FloorBand);
        floorLine = FloorLabel(FloorFontSize, FloorColour, FloorLineAt);
        floorName = FloorLabel(FloorNameFontSize, FloorNameColour, FloorNameAt);
    }

    private Label FloorLabel(int size, Color colour, float at)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = at,
            AnchorBottom = at,
            GrowVertical = Control.GrowDirection.Both,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", FloorOutline);
        FloorBand.AddChild(label);
        return label;
    }

    public void Tick(double delta, double nowSec)
    {
        ulong now = Time.GetTicksMsec();
        bool show = bifrost.DrakiPlateShown(now) && (context.Zone == DrakiTower.Zone || bifrost.DrakiPlateClosing);
        SetShown(show);
        if (show) Fill(bifrost.DrakiPlate(now));
    }

    private void SetShown(bool show)
    {
        if (shown == show) return;
        shown = show;
        Plate.Visible = show && plateLayer.Visible;
        PluginHost.Game.RaiseDraki();
    }

    internal void Fill(DrakiPlateView view)
    {
        var lines = DrakiTower.PlateLines(view, large);
        if (lines == shownLines) return;
        shownLines = lines;
        chapter.Text = lines.Chapter;
        limit.Text = lines.Limit;
        elapsed.Text = lines.Elapsed;
        Plate.ResetSize();
    }

    private void ToggleForm() => SetForm(!large);

    internal void SetForm(bool large)
    {
        this.large = large;
        formToggle.Text = large ? SmallGlyph : LargeGlyph;
        formToggle.TooltipText = large ? SmallTip : LargeTip;
        shownLines = default;
        Fill(bifrost.DrakiPlate(Time.GetTicksMsec()));
    }

    private void OnPlateInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) return;
        Plate.AcceptEvent();
        townRecall.TryOpen();
    }

    private void OnFloor(int stage, int subStage)
    {
        PluginHost.Game.RaiseDrakiFloor(stage, subStage);
        if (floorLayer.Visible) ShowFloor(DrakiTower.FloorTexts(stage, subStage));
    }

    internal void ShowFloor(DrakiFloorTexts texts)
    {
        floorLine.Text = texts.Floor;
        floorName.Text = texts.Name;
        floorTween?.Kill();
        var band = FloorBand;
        band.Modulate = new Color(1f, 1f, 1f, 0f);
        band.Visible = true;
        floorTween = context.Root.CreateTween();
        floorTween.TweenProperty(band, "modulate:a", 1f, DrakiTower.FloorFadeSeconds);
        floorTween.TweenInterval(DrakiTower.FloorHoldSeconds);
        floorTween.TweenProperty(band, "modulate:a", 0f, DrakiTower.FloorFadeSeconds);
        floorTween.TweenCallback(Callable.From(() => band.Visible = false));
    }

    private void OnClosing()
    {
        float top = context.Root.GetViewport().GetVisibleRect().Size.Y * 0.5f - CaptionFeature.TitleFontSize;
        caption.Show(ItemData.Text(DrakiTower.LastSecondsText, DrakiTower.LastSecondsFallback), LastSecondsColour,
            DrakiTower.LastSecondsNoticeSeconds - CaptionFeature.FadeOut, 0, top);
    }

    private sealed class PluginDrakiBridge(DrakiPlateFeature feature) : IGameDraki
    {
        event Action? IGameDraki.Changed { add { } remove { } }
        event Action<int, int>? IGameDraki.FloorReached { add { } remove { } }

        public bool PlateShown => feature.shown;

        public DrakiPlateView Plate => feature.bifrost.DrakiPlate(Time.GetTicksMsec());

        public DrakiRankList? Ranks => feature.board.Ranks;

        public void AskTown() => feature.townRecall.TryOpen();

        public void EnterChapter(int chapter) => feature.board.Enter(chapter);
    }
}
