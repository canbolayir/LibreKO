using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const int ClanWhSlots = 192;
    private const int ClanWhPageSize = 24;
    private const int ClanWhPages = ClanWhSlots / ClanWhPageSize;

    private readonly ItemSlot[] _clanWh = new ItemSlot[ClanWhSlots];
    private int _clanWhMoney;
    private int _clanWhPage;

    private CanvasLayer _clanWhLayer = null!;
    private HudWindow _clanWhPanel = null!;
    private bool _clanWhShown;
    private QuantityPrompt _clanWhAmount = null!;
    private bool _clanWhLoaded;
    private readonly ItemSlotView[] _clanWhCells = new ItemSlotView[ClanWhPageSize];
    private readonly ItemSlotView[] _clanWhBagCells = new ItemSlotView[28];
    private Label _clanWhPageLbl = null!, _clanWhStoredGold = null!, _clanWhCarriedGold = null!, _clanWhStatus = null!;
    private Button _clanWhDepositBtn = null!, _clanWhWithdrawBtn = null!;
    private LineEdit _clanWhGoldInput = null!;

    private struct ClanWhPending { public byte Op; public bool Gold; public bool Merge; public int InvAbs; public int WhIdx; public int To; public int Count; }
    private ClanWhPending _clanWhPending;
    private bool _clanWhInFlight;

    private void ClanWarehouseInit()
    {
        BuildClanWarehousePanel();
        Net.I.ClanWhContentsEvent += OnClanWhContents;
        Net.I.ClanWhResultEvent += OnClanWhResult;
        Net.I.GoldChangeEvent += OnClanWhGold;
    }

    private void ClanWarehouseDispose()
    {
        Net.I.ClanWhContentsEvent -= OnClanWhContents;
        Net.I.ClanWhResultEvent -= OnClanWhResult;
        Net.I.GoldChangeEvent -= OnClanWhGold;
    }

    private void OnClanWhGold(int g) { if (_clanWhShown) _clanWhCarriedGold.Text = $"{g:n0}"; }

    private void BuildClanWarehousePanel()
    {
        _clanWhLayer = new CanvasLayer { Layer = 74 };
        AddChild(_clanWhLayer);

        _clanWhPanel = new HudWindow("clanwarehouse", "Clan Warehouse") { Visible = false };
        _clanWhPanel.Closed += CloseClanWarehouse;
        _clanWhLayer.AddChild(_clanWhPanel);
        _clanWhAmount = new QuantityPrompt(76); AddChild(_clanWhAmount);
        _clanWhPanel.SetMeta("storage_amount", _clanWhAmount);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        _clanWhPanel.Body.AddChild(body);

        var whCol = new VBoxContainer();
        whCol.AddThemeConstantOverride("separation", 6);
        body.AddChild(whCol);
        whCol.AddChild(UiTheme.SectionTitle("Clan Warehouse"));

        var whGrid = new GridContainer { Columns = 4 };
        whGrid.AddThemeConstantOverride("h_separation", 4);
        whGrid.AddThemeConstantOverride("v_separation", 4);
        whCol.AddChild(whGrid);
        for (int i = 0; i < ClanWhPageSize; i++)
        {
            var cell = VaultCell(false, i);
            _clanWhCells[i] = cell;
            whGrid.AddChild(cell);
        }

        var pageRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pageRow.AddThemeConstantOverride("separation", 8);
        var prev = new Button { Name = "storage_prev", Text = "◀", FocusMode = Control.FocusModeEnum.None };
        prev.Pressed += () => ChangeClanWhPage(-1);
        _clanWhPageLbl = UiTheme.Text("1 / 8", 12, UiTheme.TextLo, HorizontalAlignment.Center);
        _clanWhPageLbl.Name = "storage_page";
        _clanWhPageLbl.CustomMinimumSize = new Vector2(60, 0);
        var next = new Button { Name = "storage_next", Text = "▶", FocusMode = Control.FocusModeEnum.None };
        next.Pressed += () => ChangeClanWhPage(1);
        pageRow.AddChild(prev); pageRow.AddChild(_clanWhPageLbl); pageRow.AddChild(next);
        whCol.AddChild(pageRow);

        whCol.AddChild(new HSeparator());
        var storedRow = new HBoxContainer();
        var sl = UiTheme.Text("Clan gold", 12, UiTheme.TextLo); sl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        storedRow.AddChild(sl);
        _clanWhStoredGold = UiTheme.Text("0", 13, UiTheme.Gold, HorizontalAlignment.Right);
        storedRow.AddChild(_clanWhStoredGold);
        whCol.AddChild(storedRow);

        var goldRow = new HBoxContainer();
        goldRow.AddThemeConstantOverride("separation", 6);
        _clanWhGoldInput = new LineEdit { PlaceholderText = "amount", CustomMinimumSize = new Vector2(110, 0) };
        goldRow.AddChild(_clanWhGoldInput);
        var depBtn = new Button { Name = "storage_deposit", Text = "Deposit", FocusMode = Control.FocusModeEnum.None };
        _clanWhDepositBtn = depBtn;
        depBtn.Pressed += () => AskClanGold(true);
        var wdrBtn = new Button { Name = "storage_withdraw", Text = "Withdraw", FocusMode = Control.FocusModeEnum.None };
        _clanWhWithdrawBtn = wdrBtn;
        wdrBtn.Pressed += () => AskClanGold(false);
        goldRow.AddChild(depBtn); goldRow.AddChild(wdrBtn);
        whCol.AddChild(goldRow);

        var bagCol = new VBoxContainer();
        bagCol.AddThemeConstantOverride("separation", 6);
        body.AddChild(bagCol);
        bagCol.AddChild(UiTheme.SectionTitle("Inventory"));

        var bagGrid = new GridContainer { Columns = 5 };
        bagGrid.AddThemeConstantOverride("h_separation", 4);
        bagGrid.AddThemeConstantOverride("v_separation", 4);
        bagCol.AddChild(bagGrid);
        for (int i = 0; i < 28; i++)
        {
            var cell = VaultBagCell(false, i);
            _clanWhBagCells[i] = cell;
            bagGrid.AddChild(cell);
        }
        bagCol.AddChild(new HSeparator());
        var carriedRow = new HBoxContainer();
        var cl = UiTheme.Text("Carried gold", 12, UiTheme.TextLo); cl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        carriedRow.AddChild(cl);
        _clanWhCarriedGold = UiTheme.Text("0", 13, UiTheme.Gold, HorizontalAlignment.Right);
        carriedRow.AddChild(_clanWhCarriedGold);
        bagCol.AddChild(carriedRow);
        _clanWhStatus = UiTheme.Text("Right-click to store / withdraw", 11, new Color(UiTheme.TextLo, 0.7f));
        _clanWhStatus.Name = "storage_status";
        bagCol.AddChild(_clanWhStatus);
    }

    private void ToggleClanWarehouse()
    {
        if (_clanWhShown) { CloseClanWarehouse(); return; }
        if (_clanWhInFlight) return;
        _clanWhLoaded = false;
        System.Array.Clear(_clanWh);
        _clanWhMoney = 0;
        _clanWhPage = 0;
        _clanWhStatus.Text = "Right-click to store / withdraw";
        _clanWhPanel.Visible = true;
        _clanWhShown = true;
        Net.I.SendClanWhOpen();
        RefreshClanWarehouse();
    }

    private void CloseClanWarehouse()
    {
        _clanWhAmount.Close();
        if (!_clanWhShown) return;
        _clanWhShown = false;
        _clanWhPanel.Visible = false;
        HideItemTooltip();
    }

    private void OnClanWhContents(int money, ItemSlot[] slots)
    {
        _clanWhMoney = money;
        _clanWhLoaded = true;
        for (int i = 0; i < ClanWhSlots && i < slots.Length; i++) _clanWh[i] = slots[i];
        if (_clanWhShown) RefreshClanWarehouse();
    }

    private void ChangeClanWhPage(int d)
    {
        _clanWhPage = _clanWhPanel.HasMeta("classic_storage")
            ? Mathf.Clamp(_clanWhPage + d, 0, ClanWhPages - 1)
            : ((_clanWhPage + d) % ClanWhPages + ClanWhPages) % ClanWhPages;
        RefreshClanWarehouse();
    }

    private void RefreshClanWarehouse()
    {
        for (int i = 0; i < ClanWhPageSize; i++)
            _clanWhCells[i].Set(_clanWh[_clanWhPage * ClanWhPageSize + i]);
        for (int i = 0; i < 28; i++)
            _clanWhBagCells[i].Set(GridStart + i < Inv.Length ? Inv[GridStart + i] : default);
        _clanWhPanel.SetMeta("storage_page", _clanWhPage);
        _clanWhPanel.SetMeta("storage_pages", ClanWhPages);
        _clanWhPageLbl.Text = $"{_clanWhPage + 1} / {ClanWhPages}";
        _clanWhPanel.SetMeta("storage_money", _clanWhMoney);
        _clanWhDepositBtn.Disabled = !_clanWhLoaded || Sheet.Gold <= 0;
        _clanWhWithdrawBtn.Disabled = !_clanWhLoaded || !Net.I.MyClan.CanInvite || _clanWhMoney <= 0;
        _clanWhWithdrawBtn.TooltipText = Net.I.MyClan.CanInvite ? "Withdraw coins" : "Only the chief or vice-chief may withdraw.";
        _clanWhStoredGold.Text = $"{_clanWhMoney:n0}";
        _clanWhCarriedGold.Text = $"{Sheet.Gold:n0}";
    }

    private void ClanWhDepositSlot(int abs) => AskVaultTransfer(false, true, abs, -1);
    private void ClanWhWithdrawSlot(int index) => AskVaultTransfer(false, false, _clanWhPage * ClanWhPageSize + index, -1);

    private void ClanWhGoldTransfer(bool deposit)
    {
        if (_clanWhInFlight || !deposit && !Net.I.MyClan.CanInvite) return;
        if (!_clanWhLoaded) { _clanWhStatus.Text = "You're not in a clan."; return; }
        if (!int.TryParse(_clanWhGoldInput.Text.Replace(",", "").Trim(), out int amount) || amount <= 0)
        { _clanWhStatus.Text = "Enter an amount."; return; }
        if (deposit && amount > Sheet.Gold) { _clanWhStatus.Text = "Not enough carried gold."; return; }
        if (!deposit && amount > _clanWhMoney) { _clanWhStatus.Text = "Not enough clan gold."; return; }

        ResetVaultStatus(false);
        _clanWhPending = new ClanWhPending { Op = (byte)(deposit ? 2 : 3), Gold = true, Count = amount };
        _clanWhInFlight = true;
        if (deposit) Net.I.SendClanWhInput(Net.GoldItemId, 0, 0, 0, amount);
        else Net.I.SendClanWhOutput(Net.GoldItemId, 0, 0, 0, amount);
    }

    private void OnClanWhResult(byte op, bool ok)
    {
        if (op == 1)
        {
            if (_clanWhShown) _clanWhStatus.Text = "You're not in a clan.";
            return;
        }

        if (!_clanWhInFlight || op != _clanWhPending.Op) return;
        _clanWhInFlight = false;
        if (!ok)
        {
            _clanWhStatus.Text = op == 3 && !Net.I.MyClan.CanInvite ? "Only the chief or vice-chief may withdraw." : "Transfer failed.";
            if (_clanWhShown) RefreshClanWarehouse();
            return;
        }

        var p = _clanWhPending;
        if (p.Op == 4)
        {
            _clanWh[p.To] = _clanWh[p.WhIdx]; _clanWh[p.WhIdx] = default;
        }
        else if (p.Gold)
        {
            if (p.Op == 2) { Sheet.Spend(p.Count); _clanWhMoney += p.Count; }
            else { _clanWhMoney -= p.Count; Sheet.Receive(p.Count); }
            _clanWhGoldInput.Clear();
            Net.I.RaiseGold(Sheet.Gold);
        }
        else if (p.Op == 2)
        {
            var bag = Inv[p.InvAbs];
            MoveStack(ref bag, ref _clanWh[p.WhIdx], p.Count, p.Merge);
            Inv[p.InvAbs] = bag;
            Net.I.MirrorInventorySlot(p.InvAbs, Inv[p.InvAbs]);
            if (CharTabOpen()) RefreshInventoryUI();
        }
        else
        {
            var bag = Inv[p.InvAbs];
            MoveStack(ref _clanWh[p.WhIdx], ref bag, p.Count, p.Merge);
            Inv[p.InvAbs] = bag;
            Net.I.MirrorInventorySlot(p.InvAbs, Inv[p.InvAbs]);
            if (CharTabOpen()) RefreshInventoryUI();
        }
        if (_clanWhShown) RefreshClanWarehouse();
    }
}
