using Godot;

namespace LibreKO.Features.Hud;

internal sealed class DurabilityFigureFeature : IFeature, ITickFeature
{
    private const string LayoutId = "hud_durability";
    private const string ArtRoot = "res://assets/ui/durability/";
    private const float FigureScale = 1.5f;
    private const float BottomLift = 220f;
    private const double PollSeconds = 0.25;
    private const int GoneText = 6068;
    private const int RecoveredText = 6064;
    private static readonly int[] PingPong = { 0, 1, 2, 3, 4, 3, 2, 1, 0 };
    private static readonly Color GoneTint = new("ff3b3b");
    private static readonly Color RecoveredTint = new("ffff00");

    private readonly IWorldContext context;
    private readonly SelfSession self;
    private readonly MiningSession mining;
    private Control? figure;

    internal Control? Figure => figure;
    private TextureRect[] worn = Array.Empty<TextureRect>();
    private TextureRect[] broken = Array.Empty<TextureRect>();
    private Texture2D[][] wornFrames = Array.Empty<Texture2D[]>();
    private float fps;
    private readonly WearState[] states = new WearState[DurabilityFigure.Parts];
    private readonly short[] lastDurability = new short[InventoryConstants.SlotMax];
    private double nextPoll;
    private double pulseSince;

    public DurabilityFigureFeature(IWorldContext context, SelfSession self, MiningSession mining)
    {
        this.context = context;
        this.self = self;
        this.mining = mining;
    }

    public void Build()
    {
        mining.ItemDurationEvent += OnItemDuration;
        using var file = Godot.FileAccess.Open(ArtRoot + "layout.json", Godot.FileAccess.ModeFlags.Read);
        var layout = file == null ? null : DurabilityFigure.ParseLayout(file.GetAsText());
        if (layout == null) return;
        var size = new Vector2(layout.Width, layout.Height) * FigureScale;
        fps = layout.Fps;

        var layer = new CanvasLayer { Layer = UiLayers.Hud };
        context.Root.AddChild(layer);
        figure = new Control
        {
            Visible = false,
            CustomMinimumSize = size,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = "Worn equipment",
        };
        layer.AddChild(figure);
        figure.AddChild(Piece(ArtRoot + "base.png", layout.Base));
        worn = new TextureRect[DurabilityFigure.Parts];
        broken = new TextureRect[DurabilityFigure.Parts];
        wornFrames = new Texture2D[DurabilityFigure.Parts][];
        for (int part = 0; part < DurabilityFigure.Parts && part < layout.Parts.Count; part++)
        {
            var spec = layout.Parts[part];
            wornFrames[part] = new Texture2D[spec.Worn.Count];
            for (int frame = 0; frame < spec.Worn.Count; frame++)
                wornFrames[part][frame] = GD.Load<Texture2D>($"{ArtRoot}worn_{part}_{frame}.png");
            worn[part] = Piece($"{ArtRoot}worn_{part}_0.png", spec.Worn[0]);
            worn[part].Visible = false;
            figure.AddChild(worn[part]);
            broken[part] = Piece($"{ArtRoot}broken_{part}.png", spec.Broken);
            broken[part].Visible = false;
            if (spec.Tooltip.Length > 0)
            {
                broken[part].TooltipText = spec.Tooltip;
                broken[part].MouseFilter = Control.MouseFilterEnum.Stop;
            }
            figure.AddChild(broken[part]);
        }
        HudLayout.Attach(figure, LayoutId, figure, () =>
        {
            var view = context.Root.GetViewport().GetVisibleRect().Size;
            return new Vector2(view.X - size.X, view.Y - BottomLift * FigureScale - size.Y);
        });
    }

    private static TextureRect Piece(string path, FigureRect rect) => new()
    {
        Texture = GD.Load<Texture2D>(path),
        Position = new Vector2(rect.X, rect.Y) * FigureScale,
        Size = new Vector2(rect.Width, rect.Height) * FigureScale,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public void Dispose() => mining.ItemDurationEvent -= OnItemDuration;

    public void Tick(double delta, double nowSec)
    {
        if (figure == null) return;
        if (nowSec >= nextPoll)
        {
            nextPoll = nowSec + PollSeconds;
            Poll(nowSec);
        }
        if (!figure.Visible || fps <= 0f) return;
        int step = PingPong[(int)((nowSec - pulseSince) * fps) % PingPong.Length];
        for (int part = 0; part < states.Length; part++)
            if (states[part] == WearState.Worn && step < wornFrames[part].Length && worn[part].Texture != wornFrames[part][step])
                worn[part].Texture = wornFrames[part][step];
    }

    private void Poll(double nowSec)
    {
        var inventory = self.Inventory;
        bool changed = false;
        bool any = false;
        int leftHandKind = inventory.Length > InventoryConstants.LeftHand ? ItemData.Get(inventory[InventoryConstants.LeftHand].ItemId)?.Kind ?? 0 : 0;
        for (int part = 0; part < DurabilityFigure.Parts; part++)
        {
            int slot = DurabilityFigure.SlotOf(part, leftHandKind);
            var state = slot < 0 || slot >= inventory.Length || inventory[slot].ItemId == 0
                ? WearState.Fine
                : DurabilityFigure.StateOf(inventory[slot].Durability, ItemData.MaxDurabilityOf(inventory[slot].ItemId));
            if (state != states[part]) changed = true;
            states[part] = state;
            any |= state != WearState.Fine;
        }
        for (int slot = 0; slot < InventoryConstants.SlotMax && slot < inventory.Length; slot++)
            lastDurability[slot] = inventory[slot].ItemId == 0 ? (short)0 : inventory[slot].Durability;
        if (!changed && figure!.Visible == any) return;
        pulseSince = nowSec;
        for (int part = 0; part < worn.Length; part++)
        {
            worn[part].Visible = states[part] == WearState.Worn;
            if (worn[part].Visible && wornFrames[part].Length > 0) worn[part].Texture = wornFrames[part][0];
            broken[part].Visible = states[part] == WearState.Broken;
        }
        figure!.Visible = any;
    }

    private void OnItemDuration(int position, short durability)
    {
        if (position < 0 || position >= InventoryConstants.SlotMax || position >= self.Inventory.Length) return;
        int itemId = self.Inventory[position].ItemId;
        if (itemId == 0) return;
        string name = ItemData.DisplayName(itemId);
        if (durability == 0)
            context.Log.Notice(GameText.System(GoneText, "The durability on %s is all gone.", name), GoneTint);
        else if (durability > lastDurability[position])
            context.Log.Notice(GameText.System(RecoveredText, "Durability of %s has recovered.", name), RecoveredTint);
        lastDurability[position] = durability;
        nextPoll = 0;
    }
}
