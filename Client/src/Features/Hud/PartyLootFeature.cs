using Godot;

namespace LibreKO.Features.Hud;

internal sealed class PartyLootFeature : IFeature, IClosableWindow
{
    private readonly IWorldContext context;
    private readonly LootSession loot;
    private readonly PartySession party;
    private readonly SelfSession self;
    private readonly PartyFeature partyHud;

    public PartyLootFeature(IWorldContext context, LootSession loot, PartySession party, SelfSession self, PartyFeature partyHud)
    {
        this.context = context;
        this.loot = loot;
        this.party = party;
        this.self = self;
        this.partyHud = partyHud;
    }

    private const string WindowId = "party_drops";
    private const string Title = "Party Drops";
    private const string EmptyLine = "Nothing has dropped for the party yet.";
    private const string ClearLabel = "Clear";
    private const string ClearTip = "Forget the drops listed so far";
    private const string TimeFormat = "HH:mm";
    private const int ListHeight = 220;
    private const int BodyWidth = 300;
    private const int IconSize = 22;
    private const int TextSize = 12;
    private const int HeadingSize = 13;

    internal PartyLootLog Log { get; } = new();

    private CanvasLayer layer = null!;
    private VBoxContainer shares = null!;
    private Label noah = null!;
    private VBoxContainer recent = null!;

    internal HudWindow Window { get; private set; } = null!;

    public bool IsOpen => Window is { Visible: true };

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.Boards };
        context.Root.AddChild(layer);
        Window = new HudWindow(WindowId, Title, new Vector2(420, 140), bodyMinWidth: BodyWidth) { Visible = false };
        Window.Closed += Close;
        layer.AddChild(Window);
        BuildBody(Window.Body);

        loot.PartyLootEvent += OnMemberLoot;
        loot.LootItemEvent += OnOwnLoot;
        loot.LootGoldEvent += OnGold;
        party.PartyRemovedEvent += OnRemoved;
        party.PartyDisbandEvent += Log.Clear;
        Log.Changed += Refresh;
        partyHud.Attach(this);
        Refresh();
    }

    public void Dispose()
    {
        loot.PartyLootEvent -= OnMemberLoot;
        loot.LootItemEvent -= OnOwnLoot;
        loot.LootGoldEvent -= OnGold;
        party.PartyRemovedEvent -= OnRemoved;
        party.PartyDisbandEvent -= Log.Clear;
        Log.Changed -= Refresh;
        partyHud.Detach(this);
    }

    private void BuildBody(VBoxContainer body)
    {
        body.AddThemeConstantOverride("separation", 6);
        shares = new VBoxContainer();
        shares.AddThemeConstantOverride("separation", 2);
        body.AddChild(shares);
        noah = UiTheme.Text("", TextSize, UiTheme.GoldBright);
        body.AddChild(noah);
        body.AddChild(UiTheme.Rule());
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, ListHeight),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body.AddChild(scroll);
        recent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        recent.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(recent);
        var clear = UiTheme.SmallButton(ClearLabel, ClearTip);
        clear.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        clear.Pressed += Log.Clear;
        body.AddChild(clear);
    }

    internal void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }
        Window.Visible = true;
        Refresh();
        partyHud.RefreshConfig();
    }

    public void Close()
    {
        if (!IsOpen) return;
        Window.Visible = false;
        partyHud.RefreshConfig();
    }

    private void OnMemberLoot(string looter, int itemId)
    {
        if (party.InParty) Log.AddItem(new PartyLootEntry(DateTime.Now, looter, itemId));
    }

    private void OnOwnLoot(int itemId)
    {
        if (party.InParty) Log.AddItem(new PartyLootEntry(DateTime.Now, self.LastEnter.Name ?? "", itemId));
    }

    private void OnGold(int total)
    {
        if (party.InParty) Log.AddNoah((long)total - self.Sheet.Gold);
    }

    private void OnRemoved(int memberId, string name)
    {
        if (memberId == context.Self.CharId) Log.Clear();
    }

    private void Refresh()
    {
        if (shares == null) return;
        foreach (var child in shares.GetChildren()) child.QueueFree();
        foreach (var child in recent.GetChildren()) child.QueueFree();
        var split = Log.Shares();
        shares.AddChild(UiTheme.Text(split.Count == 0 ? EmptyLine : "Items by member", HeadingSize, UiTheme.TextLo));
        foreach (var share in split)
            shares.AddChild(UiTheme.Text($"{share.Looter}  {share.Items}  ({share.Percent}%)", TextSize, UiTheme.TextHi));
        noah.Text = $"Your Noah from party shares: {Log.MyNoah:n0}";
        for (int i = Log.Entries.Count - 1; i >= 0; i--) recent.AddChild(EntryRow(Log.Entries[i]));
    }

    private static Control EntryRow(PartyLootEntry entry)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(UiTheme.Text(entry.At.ToString(TimeFormat), TextSize, UiTheme.TextLo));
        row.AddChild(new TextureRect
        {
            Texture = ItemIcons.Get(entry.ItemId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(IconSize, IconSize),
        });
        var text = UiTheme.Text($"{entry.Looter}: {ItemData.DisplayName(entry.ItemId)}", TextSize, UiTheme.TextHi);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.ClipText = true;
        text.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(text);
        return row;
    }
}
