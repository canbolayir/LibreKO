using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private static readonly string[] EquipViewLabels =
    {
        "Right earring", "Helmet", "Left earring", "Necklace", "Pauldron", "Pet",
        "Right hand", "Belt", "Left hand", "Right ring", "Pants", "Left ring", "Gloves", "Boots",
    };
    private static readonly (int Slot, string Label)[] EquipViewCostumes =
    {
        (InventoryConstants.CosTattoo, "Tattoo"), (InventoryConstants.CosHelmet, "Costume helmet"),
        (InventoryConstants.CosFairy, "Fairy"), (InventoryConstants.CosGloveRight, "Pathos right"),
        (InventoryConstants.CosPauldron, "Outfit"), (InventoryConstants.CosGloveLeft, "Pathos left"),
        (InventoryConstants.CosTalisman, "Talisman"), (InventoryConstants.CosWing, "Wings"),
        (InventoryConstants.CosEmblem, "Emblem"),
    };
    private CanvasLayer _equipViewLayer = null!;
    private HudWindow _equipViewPanel = null!;
    private VBoxContainer _equipViewStats = null!;
    private Label _equipViewHeader = null!, _equipViewSummary = null!, _equipViewStatus = null!;
    private readonly Dictionary<int, ItemSlotView> _equipViewCells = new();
    private bool _equipViewShown, _equipViewInFlight;
    private string _equipViewPending = "";

    private void EquipViewInit()
    {
        _equipViewLayer = new CanvasLayer { Layer = 78 }; AddChild(_equipViewLayer);
        _equipViewPanel = new HudWindow("equipview", "Equipment View", new Vector2(360, 120), 560) { Visible = false };
        _equipViewPanel.SetMeta("classic_equipview_controls", 1);
        _equipViewPanel.Closed += CloseEquipView; _equipViewLayer.AddChild(_equipViewPanel);
        var root = _equipViewPanel.Body; root.AddThemeConstantOverride("separation", 8);
        _equipViewHeader = UiTheme.Text("", 14, UiTheme.TextHi); _equipViewHeader.Name = "inspect_name"; root.AddChild(_equipViewHeader);
        _equipViewSummary = UiTheme.Text("", 12, UiTheme.TextLo); _equipViewSummary.Name = "inspect_summary"; root.AddChild(_equipViewSummary);
        var cols = new HBoxContainer(); cols.AddThemeConstantOverride("separation", 16); root.AddChild(cols);
        GridContainer Grid(string name, string caption)
        {
            var column = new VBoxContainer(); column.AddChild(UiTheme.SectionTitle(caption));
            var grid = new GridContainer { Name = name, Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4);
            column.AddChild(grid); cols.AddChild(column); return grid;
        }
        var gear = Grid("inspect_gear", "Equipment");
        for (int i = 0; i < InventoryConstants.SlotMax; i++) gear.AddChild(BuildEquipViewCell(i, EquipViewLabels[i]));
        var costume = Grid("inspect_costume", "Costume");
        foreach (var (slot, label) in EquipViewCostumes) costume.AddChild(BuildEquipViewCell(slot, label));
        var stats = new VBoxContainer { CustomMinimumSize = new Vector2(210, 0) };
        stats.AddChild(UiTheme.SectionTitle("State"));
        _equipViewStats = new VBoxContainer { Name = "inspect_stats" }; _equipViewStats.AddThemeConstantOverride("separation", 3);
        stats.AddChild(_equipViewStats); cols.AddChild(stats);
        _equipViewStatus = UiTheme.Text("", 12, UiTheme.TextLo); _equipViewStatus.Name = "inspect_status";
        _equipViewStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart; root.AddChild(_equipViewStatus);
        Net.I.EquipmentViewEvent += OnEquipmentView;
    }
    private ItemSlotView BuildEquipViewCell(int slot, string caption)
    {
        var cell = new ItemSlotView(45) { Name = "inspect_slot_" + slot, Index = slot, TooltipText = caption };
        cell.SetMeta("inspect_slot_caption", caption);
        cell.Hovered += current => ShowItemTooltip(current.Index, current.Item);
        cell.Unhovered += _ => HideItemTooltip();
        _equipViewCells[slot] = cell; return cell;
    }

    private void EquipViewDispose()
    {
        Net.I.EquipmentViewEvent -= OnEquipmentView;
    }

    private void RequestEquipmentView(string name)
    {
        if (_equipViewInFlight || string.IsNullOrWhiteSpace(name)) return;
        _equipViewInFlight = true;
        _equipViewPending = name;
        _equipViewPanel.Title = "Equipment View";
        HideItemTooltip(); ClearEquipView();
        _equipViewHeader.Text = name;
        _equipViewStatus.Text = "Requesting...";
        _equipViewStatus.AddThemeColorOverride("font_color", UiTheme.TextLo);
        _equipViewPanel.Visible = true;
        _equipViewShown = true;
        Net.I.SendEquipmentViewRequest(name);
    }

    private void CloseEquipView()
    {
        HideItemTooltip();
        _equipViewShown = false;
        _equipViewPanel.Visible = false;
    }

    private void ClearEquipView()
    {
        foreach (var cell in _equipViewCells.Values)
        {
            cell.Clear(); cell.TooltipText = cell.GetMeta("inspect_slot_caption").AsString();
        }
        ClearChildren(_equipViewStats);
        _equipViewHeader.Text = ""; _equipViewHeader.TooltipText = ""; _equipViewSummary.Text = ""; _equipViewSummary.TooltipText = "";
        _equipViewPanel.RemoveMeta("inspect_snapshot");
    }

    private void OnEquipmentView(Net.EquipmentViewResult result, Net.EquipmentView view)
    {
        if (!_equipViewInFlight) return;
        if (result == Net.EquipmentViewResult.Accepted && !string.Equals(view.Name, _equipViewPending, StringComparison.OrdinalIgnoreCase)) return;
        _equipViewInFlight = false;
        if (!_equipViewShown) return;

        if (result != Net.EquipmentViewResult.Accepted)
        {
            ClearEquipView();
            _equipViewStatus.Text = result switch
            {
                Net.EquipmentViewResult.NotInSameRegion => $"{_equipViewPending} is not in this region.",
                Net.EquipmentViewResult.CannotChooseYourself => "You cannot inspect yourself.",
                Net.EquipmentViewResult.NoViewEquipmentItem => "You need a View Equipment item.",
                _ => $"{_equipViewPending} could not be found.",
            };
            _equipViewStatus.AddThemeColorOverride("font_color", UiTheme.Bad);
            return;
        }

        ClearEquipView();
        _equipViewStatus.Text = "";
        _equipViewHeader.Text = view.Name;
        _equipViewHeader.TooltipText = view.Name;
        string level = view.RebirthLevel > 0 ? $"{view.Level}/{view.RebirthLevel}" : view.Level.ToString();
        _equipViewSummary.Text = $"Lv. {level} | {CharacterClassCatalog.DisplayName(view.Class)} | {NationName(view.Nation)}";
        _equipViewSummary.TooltipText = _equipViewSummary.Text;
        _equipViewPanel.SetMeta("inspect_snapshot", true);
        _equipViewPanel.SetMeta("inspect_target_nation", view.Nation);
        _equipViewPanel.SetMeta("inspect_target_race", view.Race);
        _equipViewPanel.SetMeta("inspect_target_face", view.Face);
        _equipViewPanel.SetMeta("inspect_target_hair", view.Hair);
        foreach (var worn in view.Worn)
            if (_equipViewCells.TryGetValue(worn.Slot, out var cell))
            {
                cell.Set(new ItemSlot { ItemId = worn.ItemId, Durability = worn.Durability, Count = 1, Flag = worn.Flag });
                cell.TooltipText = worn.ItemId == 0 ? cell.GetMeta("inspect_slot_caption").AsString() : "";
            }

        AddEquipViewStat("Max HP", view.MaxHp.ToString("N0"), new Color("ff8080"));
        AddEquipViewStat("Max MP", view.MaxMp.ToString("N0"), new Color("80c8ff"));
        _equipViewStats.AddChild(new HSeparator());
        AddEquipViewStat("Strength", StatWithBonus(view.Str, view.StrBonus));
        AddEquipViewStat("Stamina", StatWithBonus(view.Sta, view.StaBonus));
        AddEquipViewStat("Dexterity", StatWithBonus(view.Dex, view.DexBonus));
        AddEquipViewStat("Intelligence", StatWithBonus(view.Intel, view.IntelBonus));
        AddEquipViewStat("Magic attack", StatWithBonus(view.Magic, view.MagicBonus));
        _equipViewStats.AddChild(new HSeparator());
        AddEquipViewStat("Attack", view.Attack.ToString());
        AddEquipViewStat("Defence", view.Defence.ToString());
        _equipViewStats.AddChild(new HSeparator());
        AddEquipViewStat("Fire resist", view.FireR.ToString());
        AddEquipViewStat("Ice resist", view.IceR.ToString());
        AddEquipViewStat("Lightning resist", view.LightningR.ToString());
        AddEquipViewStat("Magic resist", view.MagicR.ToString());
        AddEquipViewStat("Curse resist", view.CurseR.ToString());
        AddEquipViewStat("Poison resist", view.PoisonR.ToString());
    }

    private static string StatWithBonus(int stat, int bonus) =>
        bonus > 0 ? $"{stat}  (+{bonus})" : stat.ToString();

    private void AddEquipViewStat(string label, string value, Color? color = null)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var key = UiTheme.Text(label, 12, UiTheme.TextLo);
        key.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(key);

        row.AddChild(UiTheme.Text(value, 12, color ?? UiTheme.TextHi));
        _equipViewStats.AddChild(row);
    }
}
