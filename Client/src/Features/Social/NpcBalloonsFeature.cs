using Godot;

namespace LibreKO.Features.Social;

internal sealed class NpcBalloonsFeature : IFeature, ITickFeature
{
    private const string ArtDir = "res://assets/ui/balloon/";
    private const double LifeSeconds = 7.0;
    private const double FadeSeconds = 1.0;
    private const float AnchorLeft = 100f;
    private const float AnchorAbove = 65f;
    private const int FontSize = 12;
    private const float FallbackHead = 1.8f;

    private sealed class Shouter
    {
        public NpcShoutSet Set = null!;
        public double Timer;
    }

    private sealed class Balloon
    {
        public Control Root = null!;
        public int Lines;
        public double Left = LifeSeconds;
        public Vector3? FixedAt;
        public string Said = "";
    }

    private sealed record Layout(Rect2 Top, Rect2 Middle, Rect2 Bottom, Rect2 Text, Color Colour);

    private readonly IWorldContext context;
    private readonly EntitySystem entities;
    private readonly EntitiesSession entitiesSession;
    private readonly SelfSession self;
    private readonly Dictionary<int, Shouter> shouters = new();
    private readonly Dictionary<int, int> brackets = new();
    private readonly Dictionary<int, Balloon> balloons = new();
    private readonly List<int> finished = new();
    private readonly Random rng = new();
    private CanvasLayer layer = null!;
    private Layout? layout;
    private Texture2D? top, middle, bottom;

    public NpcBalloonsFeature(IWorldContext context, EntitySystem entities, EntitiesSession entitiesSession, SelfSession self)
    {
        this.context = context;
        this.entities = entities;
        this.entitiesSession = entitiesSession;
        this.self = self;
    }

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.WorldText };
        context.Root.AddChild(layer);
        entities.EntitySpawned += OnSpawned;
        entities.EntityRemoved += OnRemoved;
        entities.EntityRevived += OnRevived;
        entitiesSession.EntityHpEvent += OnHp;
        entitiesSession.EntityHpSyncEvent += OnHpSync;
    }

    public void Dispose()
    {
        entities.EntitySpawned -= OnSpawned;
        entities.EntityRemoved -= OnRemoved;
        entities.EntityRevived -= OnRevived;
        entitiesSession.EntityHpEvent -= OnHp;
        entitiesSession.EntityHpSyncEvent -= OnHpSync;
    }

    private void OnSpawned(EntityView e, EntitySnapshot info)
    {
        if (!e.IsNpc) return;
        if (NpcTalk.Table.Monsters.ContainsKey(e.NpcId)) brackets[e.Id] = NpcTalk.NoBracket;
        if (NpcTalk.Table.ShoutsOf(e.NpcId) is { } set && NpcTalk.Shouts(info.NpcType, context.Zone, info.Nation, self.Nation))
            shouters[e.Id] = new Shouter { Set = set };
    }

    private void OnRemoved(int id)
    {
        shouters.Remove(id);
        brackets.Remove(id);
        if (balloons.TryGetValue(id, out var balloon) && balloon.FixedAt == null) Drop(id);
    }

    private void OnRevived(int id)
    {
        if (brackets.ContainsKey(id)) brackets[id] = NpcTalk.NoBracket;
    }

    private void OnHp(int id, int hp, int maxHp, int damage) => Talk(id, hp, maxHp);

    private void OnHpSync(int id, int hp, int maxHp) => Talk(id, hp, maxHp);

    private void Talk(int id, int hp, int maxHp)
    {
        if (!brackets.TryGetValue(id, out int before)) return;
        int bracket = NpcTalk.Bracket(hp, maxHp);
        if (bracket == before) return;
        brackets[id] = bracket;
        int slot = NpcTalk.Slot(bracket);
        if (!Config.MonsterBalloons || slot < 0 || !context.Entities.Views.TryGetValue(id, out var e)) return;
        if (NpcTalk.Table.TalkOf(e.NpcId, slot) is not { } line || rng.Next(NpcTalk.RollRange) >= line.Chance) return;
        Say(id, line.Text, slot == NpcTalk.DeathSlot ? HeadPoint(e) : null);
    }

    public void Tick(double delta, double nowSec)
    {
        foreach (var (id, shouter) in shouters)
        {
            shouter.Timer += delta;
            if (shouter.Timer <= shouter.Set.IntervalSeconds) continue;
            shouter.Timer = 0;
            if (NpcTalk.Pick(shouter.Set, rng.Next(NpcTalk.RollRange)) is not { Text.Length: > 0 } line) continue;
            string text = NpcTalk.Fill(line.Text, self.LastEnter.Name ?? "");
            Say(id, text, null);
            if (line.Chat && context.Entities.Views.TryGetValue(id, out var e)) context.Chat.AppendNpcShout(e.Name, text);
        }
        if (balloons.Count > 0) Place(delta);
    }

    private void Place(double delta)
    {
        var camera = context.Root.GetViewport().GetCamera3D();
        finished.Clear();
        foreach (var (id, balloon) in balloons)
        {
            balloon.Left -= delta;
            Vector3? point = balloon.FixedAt ?? (context.Entities.Views.TryGetValue(id, out var e) ? HeadPoint(e) : null);
            if (balloon.Left <= 0 || point == null || !GodotObject.IsInstanceValid(balloon.Root))
            {
                finished.Add(id);
                continue;
            }
            bool shown = camera != null && !camera.IsPositionBehind(point.Value);
            balloon.Root.Visible = shown;
            if (!shown) continue;
            var screen = camera!.UnprojectPosition(point.Value);
            balloon.Root.Position = screen - new Vector2(AnchorLeft, balloon.Lines * layout!.Middle.Size.Y + AnchorAbove);
            float alpha = (float)Mathf.Min(1.0, Mathf.Min((LifeSeconds - balloon.Left) / FadeSeconds, balloon.Left / FadeSeconds));
            balloon.Root.Modulate = new Color(1f, 1f, 1f, alpha);
        }
        foreach (int id in finished) Drop(id);
    }

    private Vector3? HeadPoint(EntityView e)
    {
        if (!GodotObject.IsInstanceValid(e.Body)) return null;
        float head = context.Entities.HeadHeight(e.Id);
        return e.Body.GlobalPosition + Vector3.Up * (head > 0f ? head : FallbackHead);
    }

    internal void Say(int id, string text, Vector3? fixedAt)
    {
        if (text.Length == 0 || balloons.TryGetValue(id, out var old) && old.Said == text) return;
        Drop(id);
        if (BuildBalloon(text, layer, out int lines) is not { } root) return;
        root.Modulate = new Color(1f, 1f, 1f, 0f);
        balloons[id] = new Balloon { Root = root, Lines = lines, FixedAt = fixedAt, Said = text };
    }

    internal Control? BuildBalloon(string text, Node parent, out int lines)
    {
        lines = 0;
        if (LoadArt() is not { } art) return null;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Position = art.Text.Position,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", art.Colour);
        parent.AddChild(root);
        root.AddChild(label);
        var font = label.GetThemeFont("font") ?? ThemeDB.FallbackFont;
        float wrap = art.Text.Size.X;
        float lineHeight = art.Middle.Size.Y;
        float fontHeight = font.GetHeight(FontSize);
        lines = Mathf.Max(1, Mathf.CeilToInt(font.GetMultilineStringSize(text, HorizontalAlignment.Left, wrap, FontSize).Y / fontHeight));
        label.AddThemeConstantOverride("line_spacing", Mathf.Max(0, Mathf.RoundToInt(lineHeight - fontHeight)));
        label.Size = new Vector2(wrap, lines * lineHeight);
        root.AddChild(Piece(top!, art.Top.Position, art.Top.Size));
        root.AddChild(Piece(middle!, art.Middle.Position, new Vector2(art.Middle.Size.X, lines * lineHeight)));
        root.AddChild(Piece(bottom!, new Vector2(art.Bottom.Position.X, art.Middle.Position.Y + lines * lineHeight), art.Bottom.Size));
        root.MoveChild(label, -1);
        root.Size = new Vector2(art.Top.End.X, art.Middle.Position.Y + lines * lineHeight + art.Bottom.Size.Y);
        return root;
    }

    private static TextureRect Piece(Texture2D texture, Vector2 position, Vector2 size) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        Texture = texture,
        Position = position,
        Size = size,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private void Drop(int id)
    {
        if (!balloons.Remove(id, out var balloon)) return;
        if (GodotObject.IsInstanceValid(balloon.Root)) balloon.Root.QueueFree();
    }

    private Layout? LoadArt()
    {
        if (layout != null) return layout;
        using var file = Godot.FileAccess.Open(ArtDir + "layout.json", Godot.FileAccess.ModeFlags.Read);
        if (file == null) return null;
        var d = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        top = GD.Load<Texture2D>(ArtDir + "top.png");
        middle = GD.Load<Texture2D>(ArtDir + "middle.png");
        bottom = GD.Load<Texture2D>(ArtDir + "bottom.png");
        layout = new Layout(RectOf(d["top"]), RectOf(d["middle"]), RectOf(d["bottom"]), RectOf(d["text"]), new Color(d["color"].AsString()));
        return layout;
    }

    private static Rect2 RectOf(Variant value)
    {
        var a = value.AsGodotArray();
        return new Rect2((float)a[0], (float)a[1], (float)a[2], (float)a[3]);
    }
}
