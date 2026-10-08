using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int PetEggKind = 150;
    private const int PetFamiliarKind = 151;
    private const int PetScrollKind = 171;
    private const int PetScrollEffect = 253;
    private const int PetNameMaxLength = 15;
    private const float PetPickSlotSize = 44f;
    private const int PetHatchTab = 0;
    private const int PetTransformTab = 1;
    private const int NoPetPick = -1;
    private const string PetTransformFailed = "The familiar could not be transformed.";

    private static readonly Dictionary<int, string> PetHatchFailures = new()
    {
        [1] = "Familiar hatching failed.",
        [2] = "Invalid name.",
        [3] = "This Familiar cannot be incubated.",
        [4] = "Limit exceeded.",
        [Net.PetHatchNameTakenCode] = "There has been a database creation error or the name is already in use. Please incubate again.",
    };

    private CanvasLayer _petHatchLayer = null!;
    private HudWindow _petHatchPanel = null!;
    private ServiceTabs _petHatchTabs = null!;
    private VBoxContainer _petHatchPage = null!;
    private VBoxContainer _petTransformPage = null!;
    private Label _petHatchEggPick = null!;
    private Label _petTransformPetPick = null!;
    private Label _petTransformScrollPick = null!;
    private LineEdit _petHatchName = null!;
    private Button _petHatchBtn = null!;
    private Label _petHatchStatus = null!;
    private int _petHatchNpc;
    private int _petHatchSlot = NoPetPick;
    private int _petTransformSlot = NoPetPick;
    private int _petScrollSlot = NoPetPick;
    private bool _petHatchInFlight;
    private bool _petHatchShown;
    private Notice? _petHatchNotice;
    private int _petHatchRevision;
    private bool PetHatchEditing => _petHatchShown && !_petHatchInFlight && _petHatchNotice == null && !_selfDead;

    private void PetHatchInit()
    {
        BuildPetHatchPanel();
        Net.I.PetHatchedEvent += OnPetHatched;
        Net.I.PetHatchFailedEvent += OnPetHatchFailed;
        Net.I.PetTransformedEvent += OnPetTransformed;
        Net.I.PetTransformFailedEvent += OnPetTransformFailed;
        Net.I.PetResetEvent += ResetPetHatch;
        Net.I.InventorySlotEvent += OnPetHatchInventorySlot;
        Net.I.InventoryGridRefreshEvent += OnPetHatchInventoryGrid;
    }

    private void PetHatchDispose()
    {
        Net.I.PetHatchedEvent -= OnPetHatched;
        Net.I.PetHatchFailedEvent -= OnPetHatchFailed;
        Net.I.PetTransformedEvent -= OnPetTransformed;
        Net.I.PetTransformFailedEvent -= OnPetTransformFailed;
        Net.I.PetResetEvent -= ResetPetHatch;
        Net.I.InventorySlotEvent -= OnPetHatchInventorySlot;
        Net.I.InventoryGridRefreshEvent -= OnPetHatchInventoryGrid;
        DismissPetHatchConfirmation();
    }

    private void BuildPetHatchPanel()
    {
        _petHatchLayer = new CanvasLayer { Layer = 74 }; AddChild(_petHatchLayer);
        _petHatchPanel = new HudWindow("pethatch", "Familiar", bodyMinWidth: 344) { Visible = false };
        _petHatchPanel.SetMeta("classic_pet_hatch_controls", 1);
        _petHatchPanel.Closed += ClosePetHatch; _petHatchLayer.AddChild(_petHatchPanel);
        var root = _petHatchPanel.Body; root.AddThemeConstantOverride("separation", 8);
        _petHatchTabs = new ServiceTabs { Name = "pet_hatch_tabs" };
        _petHatchTabs.SetTabs(new[] { "Hatch", "Transform" }, PetHatchTab);
        _petHatchTabs.Selected += _ => ShowPetHatchTab(); root.AddChild(_petHatchTabs);
        _petHatchDescription = UiTheme.Text("", 13, UiTheme.TextHi); _petHatchDescription.Name = "pet_hatch_description";
        _petHatchDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart; root.AddChild(_petHatchDescription);
        _petHatchPage = new VBoxContainer { Name = "pet_hatch_page" }; root.AddChild(_petHatchPage);
        BuildPetHatchStage(_petHatchPage, 0, "Egg", out _petHatchEggPick);
        _petHatchNameCaption = UiTheme.Text("Familiar name", 13, UiTheme.GoldBright); _petHatchNameCaption.Name = "pet_hatch_name_caption";
        _petHatchPage.AddChild(_petHatchNameCaption);
        _petHatchName = new LineEdit { Name = "pet_hatch_name", MaxLength = PetNameMaxLength, PlaceholderText = "Name", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _petHatchName.TextChanged += _ => RefreshPetHatchUI();
        _petHatchName.TextSubmitted += _ => OnPetHatchPressed(); _petHatchPage.AddChild(_petHatchName);
        _petTransformPage = new VBoxContainer { Name = "pet_transform_page" }; root.AddChild(_petTransformPage);
        var stages = new HBoxContainer(); _petTransformPage.AddChild(stages);
        BuildPetHatchStage(stages, 1, "Familiar", out _petTransformPetPick);
        BuildPetHatchStage(stages, 2, "Transformation scroll", out _petTransformScrollPick);
        _petTransformNote = UiTheme.Text("The scroll is used up to change the familiar's form.", 13, UiTheme.TextDim);
        _petTransformNote.Name = "pet_transform_note"; _petTransformNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _petTransformPage.AddChild(_petTransformNote);
        _petHatchInventoryHeading = UiTheme.SectionTitle("Inventory"); _petHatchInventoryHeading.Name = "pet_hatch_inventory_heading";
        root.AddChild(_petHatchInventoryHeading); BuildPetHatchInventory(root);
        _petHatchStatus = HudStyle.Label(13); _petHatchStatus.Name = "pet_hatch_status";
        _petHatchStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart; root.AddChild(_petHatchStatus);
        var actions = new HBoxContainer(); root.AddChild(actions);
        _petHatchBtn = UiTheme.ActionButton("Hatch", ""); _petHatchBtn.Name = "pet_hatch_accept";
        _petHatchBtn.Pressed += OnPetHatchPressed; actions.AddChild(_petHatchBtn);
        var close = UiTheme.ActionButton("Close", ""); close.Name = "pet_hatch_cancel"; close.Pressed += ClosePetHatch; actions.AddChild(close);
    }

    private void OpenPetHatch(int npcId)
    {
        if (_petHatchInFlight || _petHatchNotice != null || _petHatchShown)
        { _petHatchPanel.Visible = true; _petHatchShown = true; RefreshPetHatchUI(); return; }
        _petHatchNpc = npcId;
        _petHatchName.Text = "";
        SetPetHatchStatus("", false);
        _petHatchSlot = _petTransformSlot = _petScrollSlot = NoPetPick;
        RebuildPetPicks();
        bool transform = FirstPetPick(IsPetEgg) < 0 && FirstPetPick(IsFamiliarItem) >= 0;
        _petHatchTabs.Select(transform ? PetTransformTab : PetHatchTab, notify: false);
        ShowPetHatchTab();
        _petHatchPanel.Visible = true;
        _petHatchShown = true;
        RefreshPetHatchUI();
    }

    private void ClosePetHatch()
    {
        if (!_petHatchShown) return;
        _petHatchShown = false;
        _petHatchPanel.Visible = false;
        DismissPetHatchConfirmation();
        HideItemTooltip();
    }

    private bool PetTransforming => _petHatchTabs.Current == PetTransformTab;

    private void ShowPetHatchTab()
    {
        _petHatchPage.Visible = !PetTransforming;
        _petTransformPage.Visible = PetTransforming;
        _petHatchDescription.Text = PetTransforming ? "Transform your familiar with a scroll." : "Incubate your familiar egg.";
        _petHatchBtn.Text = PetTransforming ? "Transform" : "Hatch";
        _petHatchBtn.TooltipText = PetTransforming
            ? "Transform the chosen familiar with the chosen scroll"
            : "Hatch the chosen egg";
        SetPetHatchStatus("", false);
        RefreshPetPickLooks(); RefreshPetHatchUI();
    }

    private int FirstPetPick(Func<ItemSlot, bool> matches)
    {
        for (int abs = GridStart; abs < GridStart + GridCount && abs < Inv.Length; abs++)
            if (matches(Inv[abs])) return abs;
        return NoPetPick;
    }

    private static bool IsPetEgg(ItemSlot slot) =>
        PetPickAllowed(slot) && !slot.IsLinked && ItemData.Get(slot.ItemId) is { Kind: PetEggKind };

    private static bool IsFamiliarItem(ItemSlot slot) =>
        PetPickAllowed(slot) && slot.UniqueId != 0 && ItemData.Get(slot.ItemId) is { Kind: PetFamiliarKind };

    private static bool IsTransformScroll(ItemSlot slot) =>
        PetPickAllowed(slot) && ItemData.Get(slot.ItemId) is { Kind: PetScrollKind, Effect2: PetScrollEffect };

    private static bool PetPickAllowed(ItemSlot slot) => !slot.IsEmpty && slot.Count > 0
        && slot.State is not (ItemFlag.Sealed or ItemFlag.Duplicate or ItemFlag.Rented);

    private void RebuildPetPicks()
    {
        HideItemTooltip();
        _petHatchSlot = KeepPetPick(_petHatchSlot, IsPetEgg);
        _petTransformSlot = KeepPetPick(_petTransformSlot, IsFamiliarItem);
        _petScrollSlot = KeepPetPick(_petScrollSlot, IsTransformScroll);
        for (int i = 0; i < _petHatchInventory.Length; i++) _petHatchInventory[i].Set(GridStart + i < Inv.Length ? Inv[GridStart + i] : default);
        RefreshPetPickLooks();
    }

    private int KeepPetPick(int abs, Func<ItemSlot, bool> matches) =>
        abs >= GridStart && abs < GridStart + GridCount && abs < Inv.Length && matches(Inv[abs]) ? abs : NoPetPick;

    private void RefreshPetPickLooks()
    {
        int[] selected = { _petHatchSlot, _petTransformSlot, _petScrollSlot };
        for (int i = 0; i < selected.Length; i++)
        {
            _petHatchStage[i].Index = selected[i];
            _petHatchStage[i].Set(selected[i] >= GridStart && selected[i] < Inv.Length ? Inv[selected[i]] : default);
        }
        foreach (var cell in _petHatchInventory)
            cell.Look = (PetTransforming ? cell.Index == _petTransformSlot || cell.Index == _petScrollSlot : cell.Index == _petHatchSlot) ? SlotLook.Selected : SlotLook.Normal;
        _petHatchEggPick.Text = _petHatchSlot >= 0 ? ItemData.DisplayName(Inv[_petHatchSlot].ItemId) : "";
        _petTransformPetPick.Text = _petTransformSlot >= 0 ? FamiliarCaption(Inv[_petTransformSlot]) : "";
        _petTransformScrollPick.Text = _petScrollSlot >= 0 ? ItemData.DisplayName(Inv[_petScrollSlot].ItemId) : "";
    }

    private static string FamiliarCaption(ItemSlot slot)
    {
        string kind = ItemData.DisplayName(slot.ItemId);
        return Net.I.PetItems.TryGetValue(slot.UniqueId, out var info) ? $"{info.Name} ({kind}), level {info.Level}" : kind;
    }

    private void RefreshPetHatchUI()
    {
        bool ready = PetTransforming
            ? _petTransformSlot >= 0 && IsFamiliarItem(Inv[_petTransformSlot])
              && _petScrollSlot >= 0 && IsTransformScroll(Inv[_petScrollSlot])
            : _petHatchSlot >= 0 && IsPetEgg(Inv[_petHatchSlot]) && IsValidPetName(_petHatchName.Text);
        _petHatchBtn.Disabled = !PetHatchEditing || !ready;
        _petHatchName.Editable = PetHatchEditing;
        _petHatchTabs.SetDisabled(!PetHatchEditing);
    }

    private bool HandlePetHatchKey(InputEventKey key)
    {
        if (!_petHatchShown || !_petHatchPanel.IsVisibleInTree()
            || key.Keycode is not (Key.Enter or Key.KpEnter or Key.Escape)) return false;
        // A modal owns its own input; an unrelated text editor keeps its focus.
        if (_petHatchNotice != null) return false;
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is LineEdit or TextEdit or SpinBox && !_petHatchPanel.IsAncestorOf(focus)) return false;
        if (key.Keycode == Key.Escape) ClosePetHatch();
        else OnPetHatchPressed();
        return true;
    }

    private static bool IsValidPetName(string name)
    {
        if (name.Length is 0 or > PetNameMaxLength) return false;
        foreach (char c in name)
            if (c <= ' ' || c > '~') return false;
        return true;
    }

    private void OnPetHatchPressed()
    {
        RefreshPetHatchUI();
        if (_petHatchBtn.Disabled || !PetHatchEditing) return;
        bool transform = PetTransforming;
        int slot = transform ? _petTransformSlot : _petHatchSlot;
        int scroll = _petScrollSlot, npc = _petHatchNpc;
        var item = Inv[slot]; var material = transform ? Inv[scroll] : default;
        string name = _petHatchName.Text;
        int revision = ++_petHatchRevision;
        _petHatchNotice = Notice.Confirm(_petHatchLayer,
            transform ? "Would you like to transform this familiar?\nThe transformation scroll will be used."
                : $"Would you like to incubate the egg?\nFamiliar name: {name}",
            "Yes", "No", () => SubmitPetHatch(revision, transform, npc, slot, item, scroll, material, name),
            () => CancelPetHatchConfirmation(revision), title: transform ? "Familiar Transformation" : "Familiar Hatching");
        RefreshPetHatchUI();
    }

    private void SubmitPetHatch(int revision, bool transform, int npc, int slot, ItemSlot item, int scroll, ItemSlot material, string name)
    {
        if (revision != _petHatchRevision || !_petHatchShown || _petHatchNotice == null || _petHatchInFlight) return;
        _petHatchNotice = null;
        bool same = slot >= GridStart && slot < Inv.Length && Inv[slot].Equals(item)
            && (transform ? IsFamiliarItem(Inv[slot]) && scroll >= GridStart && scroll < Inv.Length
                && Inv[scroll].Equals(material) && IsTransformScroll(Inv[scroll]) : IsPetEgg(Inv[slot]) && IsValidPetName(name));
        if (_selfDead || !same)
        { RebuildPetPicks(); SetPetHatchStatus("Your selection changed. Please choose the items again.", true); RefreshPetHatchUI(); return; }
        _petHatchInFlight = true;
        bool sent = transform ? Net.I.SendPetTransform(npc, item.ItemId, slot - GridStart, material.ItemId, scroll - GridStart)
            : Net.I.SendPetHatch(npc, item.ItemId, slot - GridStart, name);
        if (!sent) { _petHatchInFlight = false; SetPetHatchStatus("Not connected. Please try again after reconnecting.", true); }
        else SetPetHatchStatus(transform ? "Transforming…" : "Hatching…", false);
        RefreshPetHatchUI();
    }

    private void CancelPetHatchConfirmation(int revision)
    {
        if (revision != _petHatchRevision || _petHatchNotice == null) return;
        _petHatchNotice = null; RefreshPetHatchUI();
    }

    private void DismissPetHatchConfirmation()
    {
        _petHatchRevision++;
        if (_petHatchNotice is { } notice && GodotObject.IsInstanceValid(notice)) notice.Close();
        _petHatchNotice = null;
    }

    private void ResetPetHatch()
    {
        ClosePetHatch(); DismissPetHatchConfirmation(); _petHatchInFlight = false;
        _petHatchSlot = _petTransformSlot = _petScrollSlot = NoPetPick;
        RefreshPetHatchUI();
    }

    private void OnPetHatched(int absSlot, PetItemInfo info)
    {
        _petHatchInFlight = false;
        CombatNotice($"{info.Name} hatched from the egg.");
        _petHatchName.Text = "";
        if (_petHatchShown)
        {
            RebuildPetPicks();
            SetPetHatchStatus($"{info.Name} hatched. Equip it, then use a Familiar Summon.", false);
        }
        RefreshPetHatchUI();
    }

    private void OnPetHatchFailed(int code)
    {
        _petHatchInFlight = false;
        SetPetHatchStatus(PetHatchFailures.TryGetValue(code, out var text) ? text : PetHatchFailures[1], true);
        RefreshPetHatchUI();
    }

    private void OnPetTransformed(int absSlot, PetItemInfo info)
    {
        _petHatchInFlight = false;
        string form = ItemData.DisplayName(Inv[absSlot].ItemId);
        CombatNotice($"{info.Name} transformed into {form}.");
        if (_petHatchShown)
        {
            RebuildPetPicks();
            SetPetHatchStatus($"{info.Name} transformed into {form}.", false);
        }
        RefreshPetHatchUI();
    }

    private void OnPetTransformFailed(int _)
    {
        _petHatchInFlight = false;
        SetPetHatchStatus(PetTransformFailed, true);
        RefreshPetHatchUI();
    }

    private void SetPetHatchStatus(string text, bool warn)
    {
        _petHatchStatus.Text = text;
        _petHatchStatus.Visible = text.Length > 0;
        _petHatchStatus.AddThemeColorOverride("font_color", warn ? UiTheme.Bad : UiTheme.TextHi);
    }
}
