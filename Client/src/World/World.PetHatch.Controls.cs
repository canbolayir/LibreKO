using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private readonly ItemSlotView[] _petHatchStage = new ItemSlotView[3];
    private readonly Label[] _petHatchStageHeading = new Label[3];
    private readonly ItemSlotView[] _petHatchInventory = new ItemSlotView[GridCount];
    private Label _petHatchDescription = null!, _petHatchNameCaption = null!, _petTransformNote = null!, _petHatchInventoryHeading = null!;

    private void BuildPetHatchStage(Container parent, int stage, string caption, out Label pick)
    {
        var section = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; parent.AddChild(section);
        var heading = UiTheme.Text(caption, 13, UiTheme.GoldBright); heading.Name = "pet_hatch_stage_heading_" + stage;
        _petHatchStageHeading[stage] = heading; section.AddChild(heading);
        var cell = new ItemSlotView(PetPickSlotSize) { Name = "pet_hatch_stage_" + stage, Index = NoPetPick };
        _petHatchStage[stage] = cell; section.AddChild(cell);
        cell.CanDrop = (_, data) => PetHatchDropSlot(stage, data) >= 0;
        cell.Dropped = (_, data) => { int abs = PetHatchDropSlot(stage, data); if (abs >= 0) SelectPetHatchItem(abs); };
        cell.RightClicked += _ => ClearPetHatchStage(stage);
        cell.Hovered += c => { if (c.Index >= GridStart && c.Index < Inv.Length) ShowItemTooltip(c.Index, c.Item); };
        cell.Unhovered += _ => HideItemTooltip();
        pick = UiTheme.Text("", 13, UiTheme.TextHi); pick.Name = "pet_hatch_pick_" + stage;
        pick.AutowrapMode = TextServer.AutowrapMode.WordSmart; section.AddChild(pick);
    }

    private void BuildPetHatchInventory(VBoxContainer root)
    {
        var grid = new GridContainer { Name = "pet_hatch_inventory", Columns = 7 }; root.AddChild(grid);
        grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4);
        for (int i = 0; i < _petHatchInventory.Length; i++)
        {
            var cell = new ItemSlotView(PetPickSlotSize) { Name = "pet_hatch_inventory_" + i, Index = GridStart + i };
            _petHatchInventory[i] = cell; grid.AddChild(cell);
            cell.Clicked += c => SelectPetHatchItem(c.Index); cell.RightClicked += c => SelectPetHatchItem(c.Index);
            cell.DragOut = c => PetHatchEditing && PetHatchStageFor(c.Index) >= 0
                ? (Variant)new Godot.Collections.Dictionary { ["invFrom"] = c.Index } : default(Variant);
            cell.Hovered += c => ShowItemTooltip(c.Index, c.Item); cell.Unhovered += _ => HideItemTooltip();
        }
    }

    private int PetHatchStageFor(int abs)
    {
        if (abs < GridStart || abs >= GridStart + GridCount || abs >= Inv.Length) return -1;
        if (!PetTransforming) return IsPetEgg(Inv[abs]) ? 0 : -1;
        return IsFamiliarItem(Inv[abs]) ? 1 : IsTransformScroll(Inv[abs]) ? 2 : -1;
    }

    private int PetHatchDropSlot(int stage, Variant data)
    {
        if (!PetHatchEditing || data.VariantType != Variant.Type.Dictionary) return -1;
        var values = data.AsGodotDictionary();
        if (!values.ContainsKey("invFrom") || values["invFrom"].VariantType != Variant.Type.Int) return -1;
        int abs = values["invFrom"].AsInt32(); return PetHatchStageFor(abs) == stage ? abs : -1;
    }

    private void SelectPetHatchItem(int abs)
    {
        if (!PetHatchEditing) return;
        switch (PetHatchStageFor(abs))
        {
            case 0: _petHatchSlot = abs; break;
            case 1: _petTransformSlot = abs; break;
            case 2: _petScrollSlot = abs; break;
            default: return;
        }
        SetPetHatchStatus("", false); RefreshPetPickLooks(); RefreshPetHatchUI();
    }

    private void ClearPetHatchStage(int stage)
    {
        if (!PetHatchEditing) return;
        if (stage == 0) _petHatchSlot = NoPetPick;
        else if (stage == 1) _petTransformSlot = NoPetPick;
        else if (stage == 2) _petScrollSlot = NoPetPick;
        RefreshPetPickLooks(); RefreshPetHatchUI(); HideItemTooltip();
    }

    private void OnPetHatchInventorySlot(int abs, ItemSlot item)
    {
        if (!_petHatchShown || abs < GridStart || abs >= GridStart + GridCount) return;
        RebuildPetPicks(); RefreshPetHatchUI();
    }
    private void OnPetHatchInventoryGrid(ItemSlot[] items)
    {
        if (!_petHatchShown) return;
        RebuildPetPicks(); RefreshPetHatchUI();
    }
}
