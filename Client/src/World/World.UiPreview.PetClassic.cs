using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    internal void EnablePetKeyboardUiPreview()
    {
        Chat = new ChatSystem(this); Chat.Build(); Chat.DetachNetwork();
        _worldReady = true;
        SetProcess(false); SetPhysicsProcess(false); SetProcessUnhandledInput(false);
        SetProcessInput(true);
    }

    internal bool PetKeyboardChatActive() => Chat.IsActive;
    internal void SetPetKeyboardChat(bool active) { if (active) Chat.Open(); else Chat.Close(); }

    internal CanvasLayer BuildPetClassicUiPreview(int nation)
        => BuildPetControlUiPreview(nation, false);

    internal CanvasLayer BuildPetEquipmentResponseUiPreview(int nation)
        => BuildPetControlUiPreview(nation, true);

    private CanvasLayer BuildPetControlUiPreview(int nation, bool observeInventory)
    {
        ItemData.EnsureLoaded(); SkillData.EnsureLoaded();
        var enter = Net.I.LastEnter; enter.Nation = nation; enter.Name = "FamiliarTester";
        Net.I.SeedPreviewEnter(enter);
        _ents[1] = new Ent { Id = 1, Name = "Kauly", IsNpc = true, NpcType = NpcTypes.Pet, ModelId = 25500, Level = 12 };
        _myId = 42; _selectedId = 2;
        _ents[2] = new Ent { Id = 2, Name = "Gavolt", IsNpc = true, IsMonster = true, Attackable = true, Level = 10 };
        if (observeInventory)
        {
            InventoryInit();
            _invContent.Visible = false; AddChild(_invContent);
            PetInit();
        }
        else BuildPetPanel();
        PetSkillObserveInit();
        if (!observeInventory) BuildItemTooltip();
        RemoveChild(_itemTipLayer); _petLayer.AddChild(_itemTipLayer);
        var sheet = new PetSheet { Index = 1, Name = "Kauly", Class = 101, Level = 12,
            Hp = 131, MaxHp = 168, Mp = 170, MaxMp = 190, ExpPercent = 4375,
            Satisfaction = 7240, Attack = 51, Defence = 110, Mode = PetSheet.ModeAttack };
        for (int i = 0; i < sheet.Resists.Length; i++) sheet.Resists[i] = 20 + i * 5;
        sheet.Items[0] = new ItemSlot { ItemId = PreviewAutomaticLooting, Count = 1, Durability = 1 };
        Net.I.SeedPreviewPet(sheet); OpenPet();
        if (!observeInventory) RemoveChild(_petLayer);
        return _petLayer;
    }

    internal void DisposePetEquipmentUiPreview()
    {
        GetViewport().SizeChanged -= RefreshInventoryUI;
        Net.I.ItemMoveResultEvent -= OnItemMoveResult;
        Net.I.ItemRemoveResultEvent -= OnItemRemoveResult;
        Net.I.InventorySlotEvent -= OnInventorySlotUpdate;
        Net.I.ItemGainedEvent -= OnItemGained;
        Net.I.InventoryGridRefreshEvent -= OnInventoryGridRefresh;
        Net.I.GoldChangeEvent -= OnInventoryGoldChange;
        PetDispose(); PetSkillObserveDispose();
    }
    internal CanvasLayer BuildPetHatchClassicUiPreview(int nation, bool transform)
        => BuildPetHatchUiPreview(nation, transform, false);

    internal CanvasLayer BuildPetHatchResponseUiPreview(int nation, bool transform)
        => BuildPetHatchUiPreview(nation, transform, true);

    private CanvasLayer BuildPetHatchUiPreview(int nation, bool transform, bool observeReplies)
    {
        ItemData.EnsureLoaded();
        var enter = Net.I.LastEnter; enter.Nation = nation;
        Net.I.SeedPreviewEnter(enter);
        if (observeReplies) { PetHatchInit(); BuildItemTooltip(); }
        else BuildPetHatchPanel();
        SeedFamiliarHatchPreview(transform); OpenPetHatch(PreviewTrainerNpc);
        if (transform) { SelectPetHatchItem(GridStart + 1); SelectPetHatchItem(GridStart + 9); }
        else SelectPetHatchItem(GridStart + 2);
        _petHatchName.Text = "Kauly"; RefreshPetHatchUI();
        RemoveChild(_petHatchLayer); return _petHatchLayer;
    }

    internal CanvasLayer BuildPetBarClassicUiPreview()
    {
        _petBarLayer = new CanvasLayer { Layer = 64 };
        BuildPetBar(); OnPetBarSummoned(Net.I.Pet!);
        return _petBarLayer;
    }

    private void SeedFamiliarHatchPreview(bool transform)
    {
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        if (transform)
        {
            Net.I.PetItems[PreviewPetIndex] = new PetItemInfo(PreviewPetIndex, "Kauly", 101, 12, 4200, 7300);
            Inv.ApplySlotUpdate(GridStart + 1, new ItemSlot
            {
                ItemId = PreviewKaulItem, Count = 1, Durability = 1, UniqueId = PreviewPetIndex,
            });
            Inv.ApplySlotUpdate(GridStart + 4, new ItemSlot { ItemId = PreviewImageChange, Count = 1, Durability = 1 });
            Inv.ApplySlotUpdate(GridStart + 9, new ItemSlot { ItemId = PreviewEtarothScroll, Count = 1, Durability = 1 });
        }
        else
        {
            Inv.ApplySlotUpdate(GridStart + 2, new ItemSlot { ItemId = PreviewEggItem, Count = 1, Durability = 1 });
            Inv.ApplySlotUpdate(GridStart + 6, new ItemSlot { ItemId = PreviewEggItem, Count = 1, Durability = 1 });
        }
    }

}
