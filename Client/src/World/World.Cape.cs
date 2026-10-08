using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _capeLayer = null!;
    private HudWindow _capePanel = null!;
    private const int CapePanelWidth = 460;
    private const int CapeColourColumns = 3;
    private const int CapeSwatchSize = 46;
    private const int NoCape = -1;
    private const int CapePatternSampleColour = 1;

    private GridContainer _capePatternRow = null!;
    private GridContainer _capeColourGrid = null!;
    private Label _capeChosenLbl = null!, _capeReqLbl = null!, _capePriceLbl = null!;
    private int _capePattern = -1;
    private int _capeChoice = NoCape;
    private HSlider _capeR = null!, _capeG = null!, _capeB = null!;
    private Label _capeRVal = null!, _capeGVal = null!, _capeBVal = null!;
    private Label _capeStatus = null!, _capeHint = null!;
    private bool _capePreviewing;
    private CheckBox _capeTicket = null!;
    private Button _capeBuyBtn = null!;
    private bool _capeShown;
    private bool _capeRequestInFlight;
    private LookPreview _capeLook = null!;
    private readonly Button[] _capePatterns = new Button[4], _capeColours = new Button[6];
    private readonly List<int> _capePatternIds = new(), _capeColourIds = new();
    private int _capePatternPage, _capeColourPage;
    private Label _capePatternPageLabel = null!, _capeColourPageLabel = null!;
    private Button _capePatternPrevious = null!, _capePatternNext = null!, _capeColourPrevious = null!, _capeColourNext = null!;
    private Notice? _capeNotice;
    private int _capeRevision;
    private bool CapeEditing => _capeShown && !_capeRequestInFlight && _capeNotice == null;

    private bool CapeImChief => MyClan.IsChief;

    private void CapeInit()
    {
        BuildCapePanel();
        Net.I.CapeResultEvent += OnCapeResult;
        Net.I.MyClanChangedEvent += OnCapeMyClan;
        Net.I.ClanCapeUpdateEvent += OnClanCapeUpdate;
        Net.I.ClanCapeNpcEvent += OnCapeNpc;
        Net.I.CapeResetEvent += ResetCapePicker;
    }

    private void CapeDispose()
    {
        Net.I.CapeResultEvent -= OnCapeResult;
        Net.I.MyClanChangedEvent -= OnCapeMyClan;
        Net.I.ClanCapeUpdateEvent -= OnClanCapeUpdate;
        Net.I.ClanCapeNpcEvent -= OnCapeNpc;
        Net.I.CapeResetEvent -= ResetCapePicker;
        DismissCapeConfirmation();
    }

    private void BuildCapePanel()
    {
        _capeLayer = new CanvasLayer { Layer = 74 };
        AddChild(_capeLayer);

        _capePanel = new HudWindow("cape", "Clan Cape", bodyMinWidth: CapePanelWidth)
        { Visible = false };
        _capePanel.Closed += CloseCape;
        _capePanel.SetMeta("classic_cape_controls", 1);
        _capeLayer.AddChild(_capePanel);

        var r = _capePanel.Body;
        r.AddThemeConstantOverride("separation", 8);

        _capeLook = new LookPreview(165, 217) { Name = "cape_preview" }; r.AddChild(_capeLook);
        var turn = new HBoxContainer(); r.AddChild(turn);
        foreach (int direction in new[] { -1, 1 })
        {
            var arrow = new Button { Name = direction < 0 ? "cape_turn_left" : "cape_turn_right", Text = direction < 0 ? "◀" : "▶", FocusMode = Control.FocusModeEnum.None };
            arrow.Pressed += () => _capeLook.Turn(direction * 30); turn.AddChild(arrow);
        }
        r.AddChild(UiTheme.SectionTitle("Pattern"));
        _capePatternRow = new GridContainer { Columns = 2 };
        _capePatternRow.AddThemeConstantOverride("h_separation", 4);
        _capePatternRow.AddThemeConstantOverride("v_separation", 4);
        r.AddChild(_capePatternRow);
        for (int i = 0; i < _capePatterns.Length; i++)
        {
            int slot = i;
            var button = CapeCell("cape_pattern_" + i); _capePatterns[i] = button;
            button.Pressed += () => { int index = _capePatternPage * 4 + slot; if (CapeEditing && index < _capePatternIds.Count) { ShowCapePattern(_capePatternIds[index]); UpdateCapeGate(); } };
            _capePatternRow.AddChild(button);
        }
        BuildCapePages(r, true);

        r.AddChild(UiTheme.SectionTitle("Colour"));
        var colourScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(1, 132),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        r.AddChild(colourScroll);
        _capeColourGrid = new GridContainer
        {
            Columns = CapeColourColumns,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _capeColourGrid.AddThemeConstantOverride("h_separation", 4);
        _capeColourGrid.AddThemeConstantOverride("v_separation", 4);
        colourScroll.AddChild(_capeColourGrid);
        for (int i = 0; i < _capeColours.Length; i++)
        {
            int slot = i; var button = CapeCell("cape_colour_" + i); _capeColours[i] = button;
            button.Pressed += () => { int index = _capeColourPage * 6 + slot; if (CapeEditing && index < _capeColourIds.Count) SelectCape(_capeColourIds[index]); };
            _capeColourGrid.AddChild(button);
        }
        BuildCapePages(r, false);

        var chosen = UiTheme.Section();
        chosen.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        r.AddChild(chosen);
        var chosenMargin = new MarginContainer();
        UiTheme.Margins(chosenMargin, 8, 6, 8, 6);
        chosen.AddChild(chosenMargin);
        var chosenCol = new VBoxContainer();
        chosenCol.AddThemeConstantOverride("separation", 1);
        chosenMargin.AddChild(chosenCol);
        _capeChosenLbl = UiTheme.Text("Pick a pattern, then a colour", 14, UiTheme.GoldBright);
        chosenCol.AddChild(_capeChosenLbl);
        _capeReqLbl = UiTheme.Text("Every cape has a clan rank it needs.", 12, UiTheme.TextLo);
        chosenCol.AddChild(_capeReqLbl);
        _capePriceLbl = UiTheme.Text("Dyeing the one you own costs clan points.", 12, UiTheme.TextLo);
        _capeChosenLbl.Name = "cape_chosen"; _capeReqLbl.Name = "cape_requirement"; _capePriceLbl.Name = "cape_price";
        chosenCol.AddChild(_capePriceLbl);
        chosen.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        r.AddChild(UiTheme.SectionTitle("Dye"));

        _capeR = BuildColorRow(r, "R", out _capeRVal);
        _capeG = BuildColorRow(r, "G", out _capeGVal);
        _capeB = BuildColorRow(r, "B", out _capeBVal);

        _capeTicket = new CheckBox { Text = "Pay with a castellan ticket", FocusMode = Control.FocusModeEnum.None };
        _capeTicket.Name = "cape_ticket"; _capeTicket.Toggled += _ => UpdateCapeGate();
        r.AddChild(_capeTicket);

        var actionRow = new HBoxContainer(); actionRow.AddThemeConstantOverride("separation", 8);
        _capeBuyBtn = new Button { Text = "Buy / Apply", FocusMode = Control.FocusModeEnum.None };
        _capeBuyBtn.Name = "cape_buy";
        _capeBuyBtn.Pressed += OnCapeBuyPressed;
        actionRow.AddChild(_capeBuyBtn);
        var cancel = new Button { Name = "cape_cancel", Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += CloseCape; actionRow.AddChild(cancel);
        r.AddChild(actionRow);

        _capeHint = UiTheme.Text("", 12, UiTheme.TextLo);
        _capeHint.Name = "cape_hint"; _capeHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        r.AddChild(_capeHint);
        _capeStatus = HudStyle.Label(13);
        _capeStatus.Name = "cape_status"; _capeStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        r.AddChild(_capeStatus);

        OnCapeDyeChanged();
        UpdateCapeGate();
    }

    private HSlider BuildColorRow(VBoxContainer parent, string channel, out Label valLbl)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        var lbl = HudStyle.Label(13); lbl.Text = channel; lbl.CustomMinimumSize = new Vector2(70, 0);
        row.AddChild(lbl);
        var slider = new HSlider
        {
            Name = "cape_dye_" + channel,
            MinValue = 0, MaxValue = 255, Step = 1, Value = 0,
            CustomMinimumSize = new Vector2(160, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        slider.ValueChanged += _ => { OnCapeDyeChanged(); };
        row.AddChild(slider);
        valLbl = UiTheme.Text("0", 12, UiTheme.GoldBright);
        valLbl.Name = "cape_value_" + channel;
        valLbl.CustomMinimumSize = new Vector2(34, 0);
        row.AddChild(valLbl);
        parent.AddChild(row);
        return slider;
    }

    private void OnCapeNpc()
    {
        CloseNpcDialog();
        if (!_capeShown) ToggleCape();
    }

    private void ToggleCape()
    {
        if (_capeShown) { CloseCape(); return; }
        if (_capeRequestInFlight) return;
        _capePanel.Visible = true;
        _capeShown = true;
        SetCapeStatus("", false);

        var worn = Net.I.LastEnter;
        _capeCurrent = worn.CapeId;
        _capeChoice = NoCape;
        _capePreviewing = false;
        _capeR.Value = worn.CapeR;
        _capeG.Value = worn.CapeG;
        _capeB.Value = worn.CapeB;
        _capeTicket.SetPressedNoSignal(false);
        _capeLook.ShowCape(worn.Race, worn.Face, (worn.Hair >> 24) & 0xFF, new Color((worn.Hair >> 16 & 255) / 255f, (worn.Hair >> 8 & 255) / 255f, (worn.Hair & 255) / 255f), SelfGear());

        BuildCapeCatalogue();
        UpdateCapeGate();
    }

    private void CloseCape()
    {
        if (!_capeShown) return;
        _capeShown = false;
        _capePanel.Visible = false;
        RevertCapePreview();
        DismissCapeConfirmation();
        _capeLook.Clear();
    }

    private void OnCapeBuyPressed()
    {
        if (!CapeEditing) return;
        if (!CapeImChief) { SetCapeStatus("Only the clan chief can change the cape.", true); return; }

        int capeId = _capeChoice;
        byte rr = (byte)_capeR.Value, gg = (byte)_capeG.Value, bb = (byte)_capeB.Value;
        if (capeId < 0 && rr == 0 && gg == 0 && bb == 0)
        {
            SetCapeStatus("Pick a cape, or choose a dye colour to repaint the one you have.", true);
            return;
        }

        if (!CapeCanApply()) { UpdateCapeGate(); return; }
        byte op = _capeTicket.ButtonPressed ? Net.CapeOpTicket : Net.CapeOpBuy;
        int revision = ++_capeRevision;
        string name = capeId >= 0 && Cape.TryGet(capeId, out var chosen) ? chosen.Name : "Current cape";
        _capeNotice = Notice.Confirm(_capeLayer, $"Apply {name}?\n{CapeCostText()}", "Buy", "Cancel",
            () => SubmitCape(revision, op, capeId, rr, gg, bb), () => CancelCapeConfirmation(revision), title: "Clan Cape");
        UpdateCapeGate();
    }

    private void SubmitCape(int revision, byte op, int capeId, byte rr, byte gg, byte bb)
    {
        if (revision != _capeRevision || !_capeShown || _capeRequestInFlight || _capeNotice == null) return;
        _capeNotice = null;
        if (!CapeCanApply()) { UpdateCapeGate(); return; }
        _capeRequestInFlight = true;
        SetCapeStatus("Requesting…", false);
        UpdateCapeGate();
        if (!Net.I.SendCapeBuy(op, capeId, rr, gg, bb)) OnCapeResult(false, -1, 0, 0, 0, 0);
    }

    private void OnCapeResult(bool ok, int a, int capeId, int rr, int gg, int bb)
    {
        if (!_capeRequestInFlight) return;
        _capeRequestInFlight = false;
        UpdateCapeGate();

        if (ok)
        {
            if (_capeShown)
            {
                if (capeId >= 0) { if (Cape.TryGet(capeId, out var selected)) ShowCapePattern(selected.M); SelectCape(capeId); }
                _capeR.Value = rr; _capeG.Value = gg; _capeB.Value = bb;
                OnCapeDyeChanged();
            }
            var me = Net.I.LastEnter;
            DressCape(_selfVisual, capeId >= 0 ? capeId : me.CapeId, rr, gg, bb, false, me.Race);
            _capePreviewing = _capeShown;
            _capeCurrent = capeId >= 0 ? capeId : _capeCurrent;
            string what = capeId >= 0 ? $"cape #{capeId}" : "cape dye";
            SetCapeStatus($"Applied {what}.", false);
            ChatStatusNotice($"[Clan] Cape updated ({what}).");
        }
        else
        {
            SetCapeStatus(a switch
            {
                -2 => "You're not in a clan.",
                -5 => "That cape design isn't available.",
                -6 => "Your clan's rank is too low.",
                -7 => "Not enough gold.",
                -8 => "You need a castellan ticket for that.",
                -9 => "Not enough clan points in the fund.",
                -10 => "A castellan can't use that.",
                _ => "The cape change was refused (chief-only, promoted clan, and not while busy).",
            }, true);
        }
        UpdateCapeGate();
    }

    private void OnCapeMyClan(MyClanInfo info)
    {
        if (_capeShown) UpdateCapeGate();
    }

    private bool CapeAllowed(Cape.CapeDef def) =>
        ClanTypes.MeetsCapeRank(MyClan.Flag, MyClan.Grade, def.Ranking, def.Grade);

    private static string CapeNeedName(Cape.CapeDef def) =>
        def.Ranking <= ClanTypes.Promoted && def.Grade > 0
            ? $"{CapeRankName(def.Ranking)} grade {def.Grade}"
            : CapeRankName(def.Ranking);

    private static string CapeRankName(int rank) => rank switch
    {
        <= 1 => "Clan",
        2 => "Training Knights",
        <= 7 => $"Accredited Knights grade {8 - rank}",
        <= 12 => $"Royal Knights grade {13 - rank}",
        _ => "Unknown",
    };

    private static Control CapeSwatch(int c, int m, Color dye, bool locked)
    {
        var box = new Control
        {
            CustomMinimumSize = new Vector2(CapeSwatchSize, CapeSwatchSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var cloth = Cape.ClothTexture(c);
        if (cloth != null)
        {
            var baseTex = new TextureRect
            {
                Texture = cloth,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = dye * (locked ? 0.4f : 1f),
            };
            baseTex.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            box.AddChild(baseTex);
        }
        var mark = Cape.MarkTexture(m);
        if (mark != null)
        {
            var over = new TextureRect
            {
                Texture = mark,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1f, 1f, 1f, locked ? 0.4f : 1f),
            };
            over.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            box.AddChild(over);
        }
        return box;
    }

    private void BuildCapeCatalogue()
    {
        var patterns = new SortedSet<int>();
        foreach (var (id, def) in Cape.Catalogue)
            if (CapeSoldHere(id, def)) patterns.Add(def.M);
        _capePatternIds.Clear(); _capePatternIds.AddRange(patterns);
        _capePatternPage = 0;
        ShowCapePattern(patterns.Count > 0 ? (patterns.Contains(_capePattern) ? _capePattern : patterns.Min) : -1);
    }

    private void ShowCapePattern(int pattern)
    {
        _capePattern = pattern;
        _capeColourPage = 0;
        _capeColourIds.Clear();
        foreach (var (id, def) in Cape.Catalogue)
            if (def.M == pattern && CapeSoldHere(id, def)) _capeColourIds.Add(id);
        _capeColourIds.Sort();
        // The visible pattern and the submitted cape must always belong to the same family.
        if (_capeChoice >= 0 && (!Cape.TryGet(_capeChoice, out var chosen) || chosen.M != pattern)) _capeChoice = NoCape;
        RefreshCapePages();
        if (_capeChoice >= 0) SelectCape(_capeChoice);
        else { _capeChosenLbl.Text = "Select a colour"; _capeReqLbl.Text = ""; _capePriceLbl.Text = ""; RefreshCapePreview(); }
    }

    private string CapeCellTip(Cape.CapeDef def, bool locked)
    {
        string cost = def.Points > 0 ? $"{def.Points:n0} clan points" : $"{def.Price:n0} gold";
        string need = $"needs {CapeNeedName(def)}";
        return locked ? $"{def.Name}\n{cost}\n{need} — your clan is {ClanTypes.Standing(MyClan.Flag, MyClan.Grade)}"
                      : $"{def.Name}\n{cost}\n{need}";
    }

    private int _capeCurrent = NoCape;

    private void SelectCape(int capeId)
    {
        if (!Cape.TryGet(capeId, out var def)) return;
        _capeChoice = capeId;

        if (_capePattern != def.M) ShowCapePattern(def.M);
        int index = _capeColourIds.IndexOf(capeId);
        if (index >= 0) _capeColourPage = index / 6;
        RefreshCapePages();

        _capeChosenLbl.Text = def.M > 0 ? $"{def.Name} (pattern {def.M})" : def.Name;
        _capeReqLbl.Text = CapeNeedName(def).Replace(" grade ", "\nGrade ");
        _capePriceLbl.Text = CapeCostText();

        bool locked = MyClan.InClan && !CapeAllowed(def);
        _capeReqLbl.AddThemeColorOverride("font_color", locked ? UiTheme.Bad : UiTheme.TextLo);
        RefreshCapePreview();
        UpdateCapeGate();
    }

    private void OnCapeDyeChanged()
    {
        int rr = (int)_capeR.Value, gg = (int)_capeG.Value, bb = (int)_capeB.Value;
        _capeRVal.Text = rr.ToString();
        _capeGVal.Text = gg.ToString();
        _capeBVal.Text = bb.ToString();
        RefreshCapePreview();
        if (_capeBuyBtn != null && _capeTicket != null) UpdateCapeGate();
    }

    private void RefreshCapePreview()
    {
        if (_capeBuyBtn != null)
            _capeBuyBtn.Text = _capePanel.HasMeta("classic_cape") ? (_capeChoice >= 0 ? "Buy" : "Apply") : (_capeChoice >= 0 ? "Buy cape" : "Apply dye");

        if (!_capeShown) return;

        int previewId = _capeChoice >= 0 ? _capeChoice : _capeCurrent;
        var me = Net.I.LastEnter;
        _capeLook.SetCape(previewId, new Color((float)_capeR.Value / 255f, (float)_capeG.Value / 255f, (float)_capeB.Value / 255f), me.Race);
        if (_selfVisual == null) return;
        DressCape(_selfVisual, previewId, (int)_capeR.Value, (int)_capeG.Value, (int)_capeB.Value,
            false, me.Race, highDetail: true);
        _capePreviewing = true;
    }

    private void RevertCapePreview()
    {
        if (!_capePreviewing) return;
        _capePreviewing = false;
        DressSelfCape();
    }

    private void UpdateCapeGate()
    {
        bool chief = CapeImChief;
        _capeBuyBtn.Disabled = !CapeEditing || !CapeCanApply();
        foreach (var slider in new[] { _capeR, _capeG, _capeB }) slider.Editable = CapeEditing;
        _capeTicket.Disabled = !CapeEditing;
        RefreshCapePages();
        if (_capeChoice >= 0)
        {
            _capePriceLbl.Text = CapeCostText();
            if (Cape.TryGet(_capeChoice, out var def)) _capeReqLbl.AddThemeColorOverride("font_color", CapeAllowed(def) ? UiTheme.TextLo : UiTheme.Bad);
        }
        if (!MyClan.InClan)
            _capeHint.Text = "Join a clan to buy a cape.";
        else if (!chief)
            _capeHint.Text = "Only the clan chief can change the cape.";
        else if (MyClan.Flag < ClanTypes.Promoted)
            _capeHint.Text = "Your clan must be promoted (Official) before buying a cape.";
        else if (_selfDead)
            _capeHint.Text = "You cannot change a cape while dead.";
        else if (_capeTicket.ButtonPressed)
            _capeHint.Text = "A castellan ticket is required. Only eligible capes can be purchased.";
        else
            _capeHint.Text = "Custom dye costs 36,000 clan points.";
    }

    private void SetCapeStatus(string text, bool warn)
    {
        _capeStatus.Text = text;
        _capeStatus.AddThemeColorOverride("font_color", warn ? new Color("ff6a6a") : Colors.White);
    }

    private void BuildCapeTab(VBoxContainer col)
    {
        col.AddChild(new Label { Text = "Cape" });

        var enabled = new CheckBox
        {
            Text = "Enable capes",
            ButtonPressed = Cape.Enabled,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Off frees every attached cape (self + everyone in range), "
                        + "so the cloth simulation stops costing frame time too.",
        };
        enabled.Toggled += on =>
        {
            Cape.Enabled = on;
            RefreshAllCapes();
        };
        col.AddChild(enabled);

        var dbg = new CheckBox { Text = "Show collider + drape", FocusMode = Control.FocusModeEnum.None };
        dbg.Toggled += on => Cape.DrawDebug = on;
        col.AddChild(dbg);
        col.AddChild(new Label
        {
            Text = "green = body capsules\nyellow = equipment contacts\n"
                 + "cyan = simulated cloth grid",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _capeStats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _capeStats.AddThemeColorOverride("font_color", new Color(0.7f, 0.95f, 1f));
        col.AddChild(_capeStats);
    }

    private Label? _capeStats;

    private void RefreshAllCapes()
    {
        DressSelfCape();
        foreach (var e in _ents.Values)
            if (!e.IsNpc)
                DressCape(e.Body, e.CapeId, e.CapeR, e.CapeG, e.CapeB, e.IsGm, e.Race);
    }

    private void UpdateCapeStats()
    {
        if (_capeStats == null) return;
        var cape = _selfVisual?.GetNodeOrNull<Cape>("Cape");
        _capeStats.Text = cape == null
            ? "no cape on self"
            : $"cloth {cape.Resolution}\ncollider {cape.ColliderSegments()} shapes\n"
            + $"inside body {cape.PenetrationDepth() * 100f:0.0} cm\n"
            + $"swing off drape {cape.DeviationNow() * 100f:0.0} cm";
    }

    private static (int Id, Color Dye) ResolveCape(int capeId, int r, int g, int b, bool isGm)
    {
        if (isGm || capeId == Cape.GmCapeId) return (Cape.GmCapeId, Colors.White);
        return (capeId, new Color(r / 255f, g / 255f, b / 255f));
    }

    private static void DressCape(Node3D? body, int capeId, int r, int g, int b, bool isGm, int race,
                                  bool highDetail = false)
    {
        if (body == null || !GodotObject.IsInstanceValid(body)) return;
        var (look, lookRace) = AccessoryHostFor(body, race);
        if (look != body && body.GetNodeOrNull<Cape>("Cape") is { } shed)
        {
            body.RemoveChild(shed);
            shed.QueueFree();
        }
        if (look == null) return;
        body = look;
        race = lookRace;
        var (id, dye) = ResolveCape(capeId, r, g, b, isGm);
        var existing = body.GetNodeOrNull<Cape>("Cape");
        if (!Cape.Enabled || !Cape.IsRenderable(id))
        {
            existing?.QueueFree();
            return;
        }
        // A cape freed earlier this frame is still findable; detach it so the name is free to re-attach.
        if (existing != null && existing.IsQueuedForDeletion())
        {
            body.RemoveChild(existing);
            existing = null;
        }
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            existing.SetCape(id, dye);
            return;
        }
        Cape.Attach(body, id, dye, race, highDetail);
    }

    private void DressSelfCape()
    {
        var me = Net.I.LastEnter;
        bool gm = Net.I.GmFxVisible(_myId, _isGm);
        DressCape(_selfVisual, me.CapeId, me.CapeR, me.CapeG, me.CapeB, gm, me.Race, highDetail: true);
        var (id, _) = ResolveCape(me.CapeId, me.CapeR, me.CapeG, me.CapeB, gm);
        GD.Print($"[cape] self: sent={me.CapeId} worn={(Cape.IsRenderable(id) ? id : 0)} "
               + $"dye=({me.CapeR},{me.CapeG},{me.CapeB}) gm={gm} "
               + $"cloth={_selfVisual?.GetNodeOrNull<Cape>("Cape")?.Resolution ?? "-"}");
    }

    private static void DressEntityCape(Node3D body, EntitySnapshot info)
    {
        if (info.IsNpc) return;
        DressCape(body, info.CapeId, info.CapeR, info.CapeG, info.CapeB, info.IsGm, info.Race);
    }

    private void OnClanCapeUpdate(int clanId, int capeId, int r, int g, int b)
    {
        if (MyClan.InClan && MyClan.ClanId == clanId) _capeCurrent = capeId;
        foreach (var e in _ents.Values)
            if (!e.IsNpc && e.KnightsId == clanId && e.KnightsId != 0)
            {
                e.CapeId = capeId; e.CapeR = r; e.CapeG = g; e.CapeB = b;
                DressCape(e.Body, capeId, r, g, b, e.IsGm, e.Race);
            }
        if (MyClan.InClan && MyClan.ClanId == clanId && clanId != 0)
        {
            var me = Net.I.LastEnter;
            DressCape(_selfVisual, capeId, r, g, b, Net.I.GmFxVisible(_myId, _isGm), me.Race);
        }
    }
}
