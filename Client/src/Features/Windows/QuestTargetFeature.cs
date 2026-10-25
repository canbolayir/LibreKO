using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class QuestTargetFeature : IFeature, ITickFeature
{
    private readonly IWorldContext context;
    private readonly NpcQuestSession npc;
    private readonly MapFeature map;
    private readonly SelfSession self;
    private readonly QuestBook questBook;

    public QuestTargetFeature(IWorldContext context, NpcQuestSession npcQuest, MapFeature map, SelfSession self, QuestBook questBook)
    {
        this.context = context;
        npc = npcQuest;
        this.map = map;
        this.self = self;
        this.questBook = questBook;
    }

    public void Build()
    {
        npc.QuestTargetEvent += OnTarget;
    }

    public void Dispose()
    {
        npc.QuestTargetEvent -= OnTarget;
    }

    private const int TargetMapSide = 260;
    private const double LiveInterval = 0.2;

    internal HudWindow? TargetWindow { get; private set; }
    private TargetMapView? targetMap;
    private VBoxContainer? targetText;
    private Label? targetCaption;
    private int targetZone = -1;
    private double nextLive;
    private readonly Dictionary<int, (Texture2D? Map, float Extent)> zoneMaps = new();
    private readonly List<Vector2> liveMonsters = new();

    internal void OnTarget(QuestTargetDetail target)
    {
        EnsureTargetWindow();
        TargetWindow!.Title = target.QuestTitle.Length > 0 ? target.QuestTitle : target.Target;

        foreach (var child in targetText!.GetChildren()) { targetText.RemoveChild(child); child.QueueFree(); }
        var name = target.About.Length > 0 ? target.About.Split('\n', 2)[0] : target.Target;
        var lore = target.About.Contains('\n') ? target.About.Split('\n', 2)[1] : "";
        var heading = UiTheme.Text(name, 15, UiTheme.GoldBright);
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        heading.CustomMinimumSize = new Vector2(TargetMapSide, 0);
        targetText.AddChild(heading);
        if (lore.Length > 0)
            targetText.AddChild(QuestRows.Paragraph(lore, UiTheme.TextLo, self.LastEnter.Name, TargetMapSide));

        string where = target.Where.Length > 0 ? target.Where : ZoneCatalog.Name(target.ZoneId);
        targetCaption!.Text = target.HasCoordinates ? $"{where} ({target.X}, {target.Z})" : where;
        targetZone = target.ZoneId;
        var (texture, extent) = MapOf(target.ZoneId);
        targetMap!.SetTarget(texture, extent, target.HasCoordinates ? new Vector2(target.X, target.Z) : null,
            texture == null && extent <= 0f ? $"In {ZoneCatalog.Name(target.ZoneId)}" : "");
        nextLive = 0;
        UpdateLive();
        TargetWindow.Visible = true;
    }

    private (Texture2D? Map, float Extent) MapOf(int zoneId)
    {
        if (zoneId == context.Zone) return (map.MiniMap?.MapTexture, map.MiniMap?.MapExtent ?? 0f);
        if (zoneMaps.TryGetValue(zoneId, out var known)) return known;
        var loaded = ZoneCatalog.Stem(zoneId) is { } stem
            ? (MiniMap.LoadBaked(stem), Heightmap.LoadPng(stem)?.WorldExtent ?? 0f)
            : ((Texture2D?)null, 0f);
        zoneMaps[zoneId] = loaded;
        return loaded;
    }

    public void Tick(double delta, double nowSec)
    {
        if (TargetWindow is not { Visible: true } || nowSec < nextLive) return;
        nextLive = nowSec + LiveInterval;
        UpdateLive();
    }

    private void UpdateLive()
    {
        if (targetMap == null) return;
        liveMonsters.Clear();
        if (targetZone != context.Zone || context.Self.Body == null)
        {
            targetMap.SetLive(null, Vector2.Zero, liveMonsters);
            return;
        }
        foreach (var e in context.Entities.Views.Values)
        {
            if (!e.IsNpc || e.Dead || e.Body == null || !questBook.TargetNpcs.Contains(e.NpcId)) continue;
            var (mx, mz) = Coord.ToMap(e.Body.GlobalPosition.X, e.Body.GlobalPosition.Z);
            liveMonsters.Add(new Vector2(mx, mz));
        }
        var facing = context.Self.Facing;
        var (fx, fz) = Coord.ToMap(facing.X, facing.Z);
        targetMap.SetLive(new Vector2(context.Self.MapXZ.X, context.Self.MapXZ.Z), new Vector2(fx, fz), liveMonsters);
    }

    private void EnsureTargetWindow()
    {
        if (TargetWindow != null) return;
        var layer = new CanvasLayer { Layer = UiLayers.ServiceWindows };
        context.Root.AddChild(layer);
        TargetWindow = new HudWindow("quest_target", "Target", new Vector2(320, 150), TargetMapSide) { Visible = false };
        layer.AddChild(TargetWindow);

        var body = TargetWindow.Body;
        body.AddThemeConstantOverride("separation", 8);

        var textPanel = UiTheme.Section();
        body.AddChild(textPanel);
        targetText = new VBoxContainer();
        targetText.AddThemeConstantOverride("separation", 6);
        textPanel.AddChild(targetText);

        targetCaption = UiTheme.Text("", 13, UiTheme.Gold, HorizontalAlignment.Center);
        targetCaption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        targetCaption.CustomMinimumSize = new Vector2(TargetMapSide, 0);
        body.AddChild(targetCaption);

        targetMap = new TargetMapView
        {
            CustomMinimumSize = new Vector2(TargetMapSide, TargetMapSide),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        body.AddChild(targetMap);

        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(0, 36) };
        close.Pressed += () => TargetWindow.Visible = false;
        body.AddChild(close);
    }

    private sealed partial class TargetMapView : Control
    {
        private const float SelfArrowLength = 9f;
        private const float SelfArrowWidth = 5f;
        private const float MonsterDotRadius = 3f;

        private Texture2D? tex;
        private float extent;
        private Vector2? target;
        private string note = "";
        private Vector2? selfAt;
        private Vector2 selfFacing;
        private readonly List<Vector2> monsters = new();

        public void SetTarget(Texture2D? tex, float extent, Vector2? target, string note)
        {
            this.tex = tex; this.extent = extent; this.target = target; this.note = note;
            QueueRedraw();
        }

        public void SetLive(Vector2? selfAt, Vector2 selfFacing, List<Vector2> monsters)
        {
            this.selfAt = selfAt;
            this.selfFacing = selfFacing;
            this.monsters.Clear();
            this.monsters.AddRange(monsters);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var side = Mathf.Min(Size.X, Size.Y);
            if (side <= 0f) return;
            var rect = new Rect2((Size - new Vector2(side, side)) * 0.5f, new Vector2(side, side));

            if (tex != null)
                DrawTextureRect(tex, rect, false);
            else
                DrawRect(rect, new Color(0.06f, 0.07f, 0.09f));
            DrawRect(rect, new Color(UiTheme.Gold, 0.85f), false, 2f);

            if (note.Length > 0)
            {
                var font = ThemeDB.FallbackFont;
                var width = font.GetStringSize(note, fontSize: 13).X;
                DrawString(font, rect.Position + new Vector2((side - width) * 0.5f, side * 0.5f),
                    note, HorizontalAlignment.Left, -1, 13, UiTheme.TextLo);
                return;
            }
            if (extent <= 0f) return;
            float scale = side / extent;
            Vector2 OnMap(Vector2 spot) => rect.Position + new Vector2(spot.X * scale, side - spot.Y * scale);

            foreach (var monster in monsters)
                DrawCircle(OnMap(monster), MonsterDotRadius, UiTheme.Bad);

            if (target is { } spot)
            {
                var point = OnMap(spot);
                DrawCircle(point, 11, new Color(0, 0, 0, 0.8f));
                DrawArc(point, 9, 0, Mathf.Tau, 24, UiTheme.GoldBright, 3, true);
                DrawLine(point - new Vector2(15, 0), point + new Vector2(15, 0), UiTheme.GoldBright, 2, true);
                DrawLine(point - new Vector2(0, 15), point + new Vector2(0, 15), UiTheme.GoldBright, 2, true);
            }

            if (selfAt is not { } me) return;
            var at = OnMap(me);
            var dir = new Vector2(selfFacing.X, -selfFacing.Y);
            dir = dir.LengthSquared() > 0f ? dir.Normalized() : Vector2.Up;
            var side90 = new Vector2(-dir.Y, dir.X);
            DrawColoredPolygon(new[]
            {
                at + dir * SelfArrowLength,
                at - dir * SelfArrowLength * 0.5f + side90 * SelfArrowWidth,
                at - dir * SelfArrowLength * 0.5f - side90 * SelfArrowWidth,
            }, UiTheme.Good);
        }
    }
}
