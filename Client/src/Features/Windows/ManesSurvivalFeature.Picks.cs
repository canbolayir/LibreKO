using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class ManesSurvivalFeature
{
    private const int PicksWidth = 560;
    private const float PickIconSize = 44f;
    private const float PassiveIconSize = 32f;
    private const int PassivesPerPage = 10;
    private const string PotionNote = "*Potions can be purchased by spending actual noah.";
    private const string SkipFallback = "No skill has been selected. Would you like to complete it anyway?";
    private const string PassiveTitle = "Obtain Passive Randomly";
    private const string PassiveStripTitle = "Passive";
    private const string TotalsTitle = "Total Stat for Passive";
    private const float SmallBarRight = 24f;
    private const float StripLeft = 12f;
    private const float StripTop = 0.42f;

    internal HudWindow? Picks { get; private set; }
    private VBoxContainer? pickRows;
    private VBoxContainer? potionRows;
    private Control? potionSection;
    private Button? completeButton;
    private readonly List<(ManesPick Pick, PanelContainer Row)> shownPicks = new();
    private readonly Dictionary<int, Button> potionButtons = new();
    private readonly HashSet<int> potionsWaiting = new();
    private ManesPick? passiveWon;
    private int selectedPick = -1;
    private ManesPick? pendingPick;
    private bool fullWindow = true;

    internal PanelContainer? SmallBar { get; private set; }
    private Label? smallBarText;
    private double smallBarBlinkUntil = double.NegativeInfinity;

    internal HudWindow? PassiveView { get; private set; }
    private TextureRect? passiveIcon;
    private Label? passiveName;
    private Label? passiveDesc;

    internal PanelContainer? PassiveStrip { get; private set; }
    private HBoxContainer? stripIcons;
    private Button? stripInfo;
    private Label? stripPage;
    private int stripPageIndex;

    private ManesOffer? CurrentOffer => manes.Offers.Count > 0 ? manes.Offers[0] : null;

    private void OnOffer(ManesOffer offer)
    {
        if (offer.Bonus)
            context.Log.Notice(Text(ManesSurvival.BonusOfferText, "Manes has provided you a chance to select an additional higher level skill."), Green);
        smallBarBlinkUntil = context.Now + ManesSurvival.OfferBlinkSeconds;
        if (manes.Offers.Count == 1 || Picks is not { Visible: true }) PresentOffer();
    }

    internal void PresentOffer()
    {
        if (CurrentOffer is not { } offer)
        {
            ClosePicks();
            HideSmallBar();
            return;
        }
        EnsurePicks();
        Picks!.Title = Text(offer.Bonus ? ManesSurvival.BonusTitleText : ManesSurvival.PickTitleText,
            offer.Bonus ? "Pick 1 (bonus) of Skills" : "Select 1 of the skills (excluding potions)");
        selectedPick = -1;
        shownPicks.Clear();
        foreach (var child in pickRows!.GetChildren()) child.QueueFree();
        foreach (int code in offer.Skills)
            if (Book.Pick(code) is { } pick) AddPickRow(pick);
        foreach (var child in potionRows!.GetChildren()) child.QueueFree();
        potionButtons.Clear();
        foreach (int code in offer.Potions)
            if (Book.Pick(code) is { } potion) AddPotionRow(potion);
        potionSection!.Visible = offer.Potions.Count > 0;
        RefreshSmallBar();
        if (fullWindow) ShowPicks();
        else ShowSmallBar();
        Notify();
    }

    private void ShowPicks()
    {
        fullWindow = true;
        HideSmallBar();
        Picks!.Visible = true;
        Picks.ResetSize();
        UiKit.ShowOnTop(Picks);
    }

    private void ClosePicks()
    {
        if (Picks != null && GodotObject.IsInstanceValid(Picks)) Picks.Visible = false;
    }

    internal void MinimisePicks()
    {
        fullWindow = false;
        ClosePicks();
        if (CurrentOffer != null) ShowSmallBar();
        Notify();
    }

    private void AddPickRow(ManesPick pick)
    {
        var skill = SkillData.Get(pick.Magic);
        bool held = hotbar.ManesFitOf(pick.Code, Book) == ManesFit.AlreadyHeld;
        var row = UiTheme.RowPanel(muted: held);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        row.AddChild(line);
        line.AddChild(Icon(SkillIcons.Get(pick.Magic), PickIconSize));
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddChild(UiTheme.Text(skill?.Name ?? pick.Code.ToString(), FontSize, held ? UiTheme.TextLo : UiTheme.GoldBright));
        var desc = UiTheme.Text(skill?.Desc ?? "", 11, UiTheme.TextLo);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(300, 0);
        words.AddChild(desc);
        line.AddChild(words);
        var choose = UiTheme.SmallButton(held ? "Select" : "Fortify", held ? "Your bar already holds this skill or a stronger one" : "Choose this skill");
        choose.Disabled = held;
        choose.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        int index = shownPicks.Count;
        choose.Pressed += () => SelectPick(index);
        line.AddChild(choose);
        pickRows!.AddChild(row);
        shownPicks.Add((pick, row));
    }

    private void AddPotionRow(ManesPick potion)
    {
        var row = UiTheme.RowPanel();
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        row.AddChild(line);
        line.AddChild(Icon(ItemIcons.Get(potion.Item), PickIconSize));
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddChild(UiTheme.Text($"{ItemData.DisplayName(potion.Item)} X {potion.Count}", FontSize, UiTheme.TextHi));
        words.AddChild(UiTheme.Text(TextTemplate.Fill(Text(ManesSurvival.NoahPriceText, "%d noah"), potion.Price), 11, UiTheme.Gold));
        line.AddChild(words);
        var buy = UiTheme.SmallButton("Purchase", "Buy with noah");
        buy.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        int code = potion.Code;
        buy.Disabled = potionsWaiting.Contains(code);
        buy.Pressed += () => BuyPotion(code);
        line.AddChild(buy);
        potionButtons[code] = buy;
        potionRows!.AddChild(row);
    }

    private static TextureRect Icon(Texture2D? texture, float size) => new()
    {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    internal void SelectPick(int index)
    {
        selectedPick = index;
        for (int i = 0; i < shownPicks.Count; i++)
            shownPicks[i].Row.AddThemeStyleboxOverride("panel", UiTheme.Row(selected: i == index));
        Notify();
    }

    internal void BuyPotion(int code)
    {
        if (!potionsWaiting.Add(code)) return;
        if (potionButtons.TryGetValue(code, out var button) && GodotObject.IsInstanceValid(button)) button.Disabled = true;
        requests.BuyPotion(code);
        Notify();
    }

    internal void ReopenPicks()
    {
        if (CurrentOffer == null) return;
        fullWindow = true;
        PresentOffer();
    }

    internal void ClosePassiveWon()
    {
        passiveWon = null;
        if (PassiveView != null && GodotObject.IsInstanceValid(PassiveView)) PassiveView.Visible = false;
        Notify();
    }

    internal void Complete()
    {
        if (CurrentOffer is not { } offer) return;
        if (selectedPick < 0 || selectedPick >= shownPicks.Count)
        {
            Notice.Confirm(context.Root, Text(ManesSurvival.SkipPickText, SkipFallback), "Yes", "No", () =>
            {
                requests.SkipPick();
                NextOffer();
            });
            return;
        }
        pendingPick = shownPicks[selectedPick].Pick;
        if (offer.Bonus) requests.BonusPick(pendingPick.Code);
        else requests.Pick(pendingPick.Code);
        NextOffer();
    }

    private void NextOffer()
    {
        manes.TakeOffer();
        if (CurrentOffer == null)
        {
            ClosePicks();
            HideSmallBar();
            Notify();
            return;
        }
        PresentOffer();
    }

    private void OnPickReply(ManesPickReply reply)
    {
        if (reply.Action == ManesPickAction.Potion && !reply.Bonus)
        {
            potionsWaiting.Clear();
            foreach (var button in potionButtons.Values)
                if (GodotObject.IsInstanceValid(button)) button.Disabled = false;
            Notify();
        }
        if (reply.Result != ManesSurvivalWire.PickSucceeded)
        {
            string failure = ManesSurvival.PickFailure(reply.Result, Text);
            if (failure.Length > 0) context.Log.Notice(failure, Refusal);
            return;
        }
        if (reply.Action != ManesPickAction.Skill || pendingPick is not { } pick) return;
        pendingPick = null;
        if (pick.Kind == ManesSurvival.PassiveKind)
        {
            manes.KeepPassive(pick.Code);
            RefreshPassiveStrip();
        }
        else hotbar.PlaceManesSkill(pick, Book);
        Notify();
    }

    private void EnsurePicks()
    {
        if (Picks != null && GodotObject.IsInstanceValid(Picks)) return;
        Picks = new HudWindow("manes_picks", "", bodyMinWidth: PicksWidth) { Visible = false };
        Picks.Closed += MinimisePicks;
        Layer().AddChild(Picks);
        var body = Picks.Body;
        body.AddThemeConstantOverride("separation", 8);

        pickRows = new VBoxContainer();
        pickRows.AddThemeConstantOverride("separation", 4);
        body.AddChild(pickRows);

        var potions = new VBoxContainer();
        potions.AddThemeConstantOverride("separation", 4);
        potions.AddChild(UiTheme.SectionTitle("Potions"));
        potionRows = new VBoxContainer();
        potionRows.AddThemeConstantOverride("separation", 4);
        potions.AddChild(potionRows);
        potions.AddChild(UiTheme.Text(PotionNote, 11, UiTheme.TextLo));
        potionSection = potions;
        body.AddChild(potions);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        completeButton = UiTheme.ActionButton("Complete", "Take the selected skill");
        completeButton.Pressed += Complete;
        footer.AddChild(completeButton);
        body.AddChild(footer);
    }

    private void ShowSmallBar()
    {
        EnsureSmallBar();
        RefreshSmallBar();
        SmallBar!.Visible = true;
    }

    private void HideSmallBar()
    {
        if (SmallBar != null && GodotObject.IsInstanceValid(SmallBar)) SmallBar.Visible = false;
    }

    private void RefreshSmallBar()
    {
        if (smallBarText == null || !GodotObject.IsInstanceValid(smallBarText)) return;
        smallBarText.Text = TextTemplate.Fill(Text(ManesSurvival.AcquirableText, "Acquirable Skill : %d"), manes.Offers.Count);
    }

    private void SmallBarTick()
    {
        if (SmallBar == null || !GodotObject.IsInstanceValid(SmallBar) || !SmallBar.Visible) return;
        bool blinking = context.Now < smallBarBlinkUntil;
        SmallBar.Modulate = blinking && (int)context.Now % 2 == 0 ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
    }

    private void EnsureSmallBar()
    {
        if (SmallBar != null && GodotObject.IsInstanceValid(SmallBar)) return;
        SmallBar = new PanelContainer { Visible = false };
        SmallBar.AddThemeStyleboxOverride("panel", QuestProgressToastFeature.ToastStyle());
        SmallBar.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        SmallBar.GrowHorizontal = Control.GrowDirection.Begin;
        SmallBar.GrowVertical = Control.GrowDirection.Both;
        SmallBar.OffsetRight = -SmallBarRight;
        Layer().AddChild(SmallBar);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        SmallBar.AddChild(line);
        smallBarText = UiTheme.Text("", FontSize, UiTheme.GoldBright);
        smallBarText.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(smallBarText);
        var open = UiTheme.SmallButton("Open", "Choose a skill");
        open.Pressed += () =>
        {
            if (CurrentOffer == null) return;
            fullWindow = true;
            PresentOffer();
        };
        line.AddChild(open);
    }

    private void OnPassive(int code)
    {
        RefreshPassiveStrip();
        if (Book.Pick(code) is not { } pick) return;
        passiveWon = pick;
        Notify();
        EnsurePassiveView();
        var skill = SkillData.Get(pick.Magic);
        passiveIcon!.Texture = SkillIcons.Get(pick.Magic);
        passiveName!.Text = skill?.Name ?? "";
        passiveDesc!.Text = skill?.Desc ?? "";
        PassiveView!.Visible = true;
        PassiveView.ResetSize();
        UiKit.ShowOnTop(PassiveView);
    }

    private void EnsurePassiveView()
    {
        if (PassiveView != null && GodotObject.IsInstanceValid(PassiveView)) return;
        PassiveView = new HudWindow("manes_passive", PassiveTitle, bodyMinWidth: 360) { Visible = false };
        PassiveView.Closed += ClosePassiveWon;
        Layer().AddChild(PassiveView);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        passiveIcon = Icon(null, PickIconSize);
        line.AddChild(passiveIcon);
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        passiveName = UiTheme.Text("", FontSize, UiTheme.GoldBright);
        passiveDesc = UiTheme.Text("", 11, UiTheme.TextLo);
        passiveDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        passiveDesc.CustomMinimumSize = new Vector2(260, 0);
        words.AddChild(passiveName);
        words.AddChild(passiveDesc);
        line.AddChild(words);
        PassiveView.Body.AddChild(line);
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var done = UiTheme.ActionButton("Complete", "Close");
        done.Pressed += ClosePassiveWon;
        footer.AddChild(done);
        PassiveView.Body.AddChild(footer);
    }

    internal void RefreshPassiveStrip()
    {
        if (!manes.Running || manes.Passives.Count == 0)
        {
            HidePassiveStrip();
            return;
        }
        EnsurePassiveStrip();
        var counts = new List<(int Code, int Count)>();
        foreach (int code in manes.Passives)
        {
            int at = counts.FindIndex(c => c.Code == code);
            if (at >= 0) counts[at] = (code, counts[at].Count + 1);
            else counts.Add((code, 1));
        }
        int pages = Math.Max(1, (counts.Count + PassivesPerPage - 1) / PassivesPerPage);
        stripPageIndex = Math.Clamp(stripPageIndex, 0, pages - 1);
        foreach (var child in stripIcons!.GetChildren()) child.QueueFree();
        foreach (var (code, count) in counts.Skip(stripPageIndex * PassivesPerPage).Take(PassivesPerPage))
        {
            var pick = Book.Pick(code);
            var cell = new Control { CustomMinimumSize = new Vector2(PassiveIconSize, PassiveIconSize) };
            var icon = Icon(pick == null ? null : SkillIcons.Get(pick.Magic), PassiveIconSize);
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            cell.AddChild(icon);
            cell.TooltipText = pick == null ? "" : SkillData.Get(pick.Magic)?.Name ?? "";
            if (count > 1)
            {
                var badge = UiTheme.Text(count.ToString(), 10, Colors.White, HorizontalAlignment.Right);
                badge.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
                badge.GrowHorizontal = Control.GrowDirection.Begin;
                badge.GrowVertical = Control.GrowDirection.Begin;
                cell.AddChild(badge);
            }
            stripIcons.AddChild(cell);
        }
        stripPage!.Text = $"{stripPageIndex + 1}/{pages}";
        var totals = ManesSurvival.Totals(manes.Passives.Select(c => Book.Pick(c)).OfType<ManesPick>());
        stripInfo!.TooltipText = string.Join("\n",
            TotalsTitle,
            $"Maximum HP: {TextTemplate.Fill(Text(ManesSurvival.IncreaseText, "%d%% increase"), totals.Hp)}",
            $"Attack Power: {TextTemplate.Fill(Text(ManesSurvival.IncreaseText, "%d%% increase"), totals.AttackUp)}",
            $"Reduce Damage: {TextTemplate.Fill(Text(ManesSurvival.DecreaseText, "%d%% decrease"), totals.AttackDown)}");
        PassiveStrip!.Visible = true;
        PassiveStrip.ResetSize();
    }

    private void HidePassiveStrip()
    {
        if (PassiveStrip != null && GodotObject.IsInstanceValid(PassiveStrip)) PassiveStrip.Visible = false;
    }

    private void EnsurePassiveStrip()
    {
        if (PassiveStrip != null && GodotObject.IsInstanceValid(PassiveStrip)) return;
        PassiveStrip = new PanelContainer { Visible = false };
        PassiveStrip.AddThemeStyleboxOverride("panel", QuestProgressToastFeature.ToastStyle());
        PassiveStrip.AnchorTop = PassiveStrip.AnchorBottom = StripTop;
        PassiveStrip.OffsetLeft = StripLeft;
        PassiveStrip.GrowVertical = Control.GrowDirection.Both;
        Layer().AddChild(PassiveStrip);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        PassiveStrip.AddChild(column);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 6);
        var title = UiTheme.Text(PassiveStripTitle, FontSize, UiTheme.GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(title);
        var back = UiTheme.IconButton("◀", "Previous page");
        back.Pressed += () => { stripPageIndex--; RefreshPassiveStrip(); };
        head.AddChild(back);
        stripPage = UiTheme.Text("", 11, UiTheme.TextLo);
        head.AddChild(stripPage);
        var next = UiTheme.IconButton("▶", "Next page");
        next.Pressed += () => { stripPageIndex++; RefreshPassiveStrip(); };
        head.AddChild(next);
        stripInfo = UiTheme.IconButton("i", TotalsTitle);
        head.AddChild(stripInfo);
        column.AddChild(head);
        stripIcons = new HBoxContainer();
        stripIcons.AddThemeConstantOverride("separation", 3);
        column.AddChild(stripIcons);
    }
}
