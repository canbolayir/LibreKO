using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class ManesSurvivalFeature : IFeature, ITickFeature
{
    private readonly IWorldContext context;
    private readonly ManesSurvivalSession manes;
    private readonly SelfSession self;
    private readonly IManesSurvivalRequests requests;
    private readonly MapFeature map;
    private readonly CaptionFeature caption;
    private readonly HotbarFeature hotbar;
    private readonly EntityPlatesSystem entityPlates;

    public ManesSurvivalFeature(IWorldContext context, ManesSurvivalSession manes, SelfSession self, IManesSurvivalRequests requests, MapFeature map, CaptionFeature caption, HotbarFeature hotbar, EntityPlatesSystem entityPlates)
    {
        this.context = context;
        this.manes = manes;
        this.self = self;
        this.requests = requests;
        this.map = map;
        this.caption = caption;
        this.hotbar = hotbar;
        this.entityPlates = entityPlates;
    }

    private const string TablePath = "res://assets/ui/manes_survival.json";
    private const int FontSize = 13;
    private const float MarkerRadius = 6f;

    private static readonly Color Yellow = new(1f, 1f, 0f);
    private static readonly Color Green = new(0f, 1f, 0f);
    private static readonly Color Refusal = new("ff3b3b");
    private static readonly Color MarkerColour = new("ff6040");

    private ManesBook book = ManesBook.Empty;
    private CanvasLayer? layer;
    private readonly List<ManesMarker> markers = new();
    private double markersUntil = double.NegativeInfinity;

    private bool InZone => ManesSurvival.IsZone(context.Zone);

    private bool namesHidden;
    private const int NoZone = -1;
    private int refusalZone = NoZone;

    internal string? ChatRefusal() => InZone ? "" : null;

    internal ManesBook Book
    {
        get
        {
            if (book.Picks.Length == 0 && Godot.FileAccess.FileExists(TablePath))
                book = ManesSurvival.Parse(Godot.FileAccess.GetFileAsString(TablePath));
            return book;
        }
    }

    private static string Text(int id, string fallback) => ItemData.Text(id, fallback);

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.EventPanels };
        context.Root.AddChild(layer);
        context.Hud.PluginSeam(layer, HudPart.ManesSurvival);
        PluginHost.Game.AttachManesSurvival(new PluginManesBridge(this));
        map.AddMiniSource(AddMarkers);
        map.HidePlayerDotsWhen(() => InZone && !self.IsGm);
        HudWindow.Refused = id => ManesSurvival.RefusesWindow(context.Zone, id);
        namesHidden = entityPlates.HidesHunters;
        manes.SignUpEvent += OnSignUp;
        manes.StartEvent += OnStart;
        manes.BossEvent += OnBoss;
        manes.RankEvent += OnRank;
        manes.EndEvent += OnEnd;
        manes.MarkersEvent += OnMarkers;
        manes.LastBossEvent += OnLastBoss;
        manes.KillEvent += OnKill;
        manes.RewardEvent += OnReward;
        manes.InvincibleEvent += OnInvincible;
        manes.ScoreEvent += OnScore;
        manes.BagItemEvent += OnBagItem;
        manes.BagReplyEvent += OnBagReply;
        manes.OfferEvent += OnOffer;
        manes.PickReplyEvent += OnPickReply;
        manes.PassiveEvent += OnPassive;
        if (manes.Board.Open && manes.Board.EndsAt > context.Now) ShowBoard();
        if (manes.Running && InZone)
        {
            RefreshPassiveStrip();
            if (manes.Offers.Count > 0) ShowSmallBar();
        }
    }

    public void Dispose()
    {
        HudWindow.Refused = null;
        manes.SignUpEvent -= OnSignUp;
        manes.StartEvent -= OnStart;
        manes.BossEvent -= OnBoss;
        manes.RankEvent -= OnRank;
        manes.EndEvent -= OnEnd;
        manes.MarkersEvent -= OnMarkers;
        manes.LastBossEvent -= OnLastBoss;
        manes.KillEvent -= OnKill;
        manes.RewardEvent -= OnReward;
        manes.InvincibleEvent -= OnInvincible;
        manes.ScoreEvent -= OnScore;
        manes.BagItemEvent -= OnBagItem;
        manes.BagReplyEvent -= OnBagReply;
        manes.OfferEvent -= OnOffer;
        manes.PickReplyEvent -= OnPickReply;
        manes.PassiveEvent -= OnPassive;
    }

    public void Tick(double delta, double nowSec)
    {
        BoardTick();
        BarTick();
        SmallBarTick();
        RankTick();
        if (markers.Count > 0 && context.Now >= markersUntil) markers.Clear();
        CloseRefusedWindowsOnEntry();
        bool hidden = entityPlates.HidesHunters;
        if (hidden == namesHidden) return;
        namesHidden = hidden;
        entityPlates.RefreshHunterNames();
    }

    private void CloseRefusedWindowsOnEntry()
    {
        if (!InZone)
        {
            refusalZone = NoZone;
            return;
        }
        if (refusalZone == context.Zone) return;
        refusalZone = context.Zone;
        foreach (var id in ManesSurvival.RefusedWindows)
            if (HudWindow.Find(id) is { Visible: true } window) window.Close();
    }

    private CanvasLayer Layer()
    {
        if (layer != null && GodotObject.IsInstanceValid(layer)) return layer;
        layer = new CanvasLayer { Layer = UiLayers.EventPanels };
        context.Root.AddChild(layer);
        return layer;
    }

    private void OnStart(ManesStart start)
    {
        hotbar.StartManes();
        HideBoard();
        StartBar(start.Seconds);
        caption.Show(Text(ManesSurvival.NoBattleText, "You cannot battle with other users for 3 minutes."), Yellow, CentreHold, CentreFade, CentreTop);
        foreach (int id in ManesSurvival.StartTexts)
            context.Log.Notice(Text(id, ""), Yellow);
        RefreshPassiveStrip();
        context.Inventory.Refresh();
        Notify();
    }

    private void OnEnd(ManesEnd end)
    {
        HideBar();
        ClosePicks();
        HideSmallBar();
        HidePassiveStrip();
        HideRank();
        markers.Clear();
        context.Inventory.Refresh();
        Notify();
    }

    private void OnMarkers(IReadOnlyList<ManesMarker> points)
    {
        markers.Clear();
        markers.AddRange(points);
        markersUntil = context.Now + ManesSurvival.MarkerSeconds;
    }

    private void AddMarkers(List<MiniMap.Blip> blips)
    {
        if (!InZone) return;
        foreach (var m in markers)
            blips.Add(new MiniMap.Blip(m.X, m.Z, MarkerColour, MarkerRadius, true));
    }

    private void LogMarkup(string raw, Color colour)
    {
        string me = self.LastEnter.Name ?? "";
        context.Log.Add(QuestMarkup.Plain(raw, me), CombatLogKind.Status, colour, QuestMarkup.Rich(raw, me));
    }

    private static string FontOpen(int nation) => $"<font color=@{ManesSurvival.NationColour(nation)}@>";

    private void OnLastBoss(string name)
    {
        if (name.Length == 0) return;
        LogMarkup(TextTemplate.Fill(Text(ManesSurvival.LastBossText, "Last boss has been defeated by %s"), name), Yellow);
    }

    private void OnKill(ManesKill kill)
    {
        string raw = TextTemplate.Fill(Text(ManesSurvival.KillText, "%s%s</font> defeated %s%s</font> and scored %d Points"),
            FontOpen(kill.KillerNation), kill.Killer, FontOpen(kill.VictimNation), kill.Victim, kill.Points);
        LogMarkup(raw, Yellow);
    }

    private void OnReward(ManesReward reward)
    {
        if (ItemData.Get(reward.ItemId) == null) return;
        string item = ItemData.DisplayName(reward.ItemId);
        string line = reward.BossKiller
            ? TextTemplate.Fill(Text(ManesSurvival.BossRewardText, "Final boss has been defeated so earned %s"), item)
            : TextTemplate.Fill(Text(ManesSurvival.RankRewardText, "Achieved %d rank so you received %s%d. (Total Score: %d)"),
                reward.Rank, item, reward.Count, reward.TotalScore);
        context.Log.Notice(line, Yellow);
    }

    private void OnInvincible()
    {
        string text = Text(ManesSurvival.InvincibleTimeText, "It's invincible time.");
        context.Chat?.ShowNoticePreview(text);
        context.Log.Notice(text, Refusal);
    }

    private void OnBagItem(ManesBagItem item) => context.Inventory.Refresh();

    private void OnBagReply(bool done) => context.Inventory.Refresh();
}
