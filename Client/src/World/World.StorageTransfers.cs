using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private bool StorageTransferBusy => _whInFlight || _vipWhInFlight || _clanWhInFlight || _moveInFlight || _moveQueue.Count > 0;
    private bool StorageBagMove(int target, Variant data) => !StorageTransferBusy && data.VariantType == Variant.Type.Dictionary &&
        data.AsGodotDictionary().ContainsKey("invFrom") && data.AsGodotDictionary()["invFrom"].AsInt32() != target &&
        InMainBag(data.AsGodotDictionary()["invFrom"].AsInt32());
    private ItemSlotView StorageBagCell(int index, System.Action<int> take,
        System.Func<int, Variant, bool> accepts, System.Action<int, Variant> drop)
    {
        var cell = new ItemSlotView(44) { Index = GridStart + index, Name = "storage_bag_" + index };
        cell.RightClicked += c => take(c.Index);
        cell.Hovered += c => ShowItemTooltip(c.Index, c.Item);
        cell.Unhovered += _ => HideItemTooltip();
        cell.DragOut = c => new Godot.Collections.Dictionary { { "invFrom", c.Index } };
        cell.CanDrop = (c, data) => !StorageTransferBusy && (StorageBagMove(c.Index, data) || accepts(c.Index, data));
        cell.Dropped = (c, data) =>
        {
            if (StorageBagMove(c.Index, data)) MoveBetween(data.AsGodotDictionary()["invFrom"].AsInt32(), c.Index);
            else if (!StorageTransferBusy) drop(c.Index, data);
        };
        return cell;
    }

    private bool CanDropOnWhBag(int abs, Variant data) => !_whInFlight &&
        data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("companionFrom") &&
        data.AsGodotDictionary()["companionFrom"].AsInt32() is >= 0 and < WhSlots;
    private void DropOnWhBag(int abs, Variant data) => AskWithdraw(data.AsGodotDictionary()["companionFrom"].AsInt32(), abs);

    private ItemSlotView VaultBagCell(bool vip, int index) => StorageBagCell(index,
        abs => AskVaultTransfer(vip, true, abs, -1),
        (abs, data) => VaultAccepts(vip, true, data),
        (abs, data) => AskVaultTransfer(vip, false, data.AsGodotDictionary()["vaultFrom"].AsInt32(), abs));

    private ItemSlotView VaultCell(bool vip, int index)
    {
        int Absolute() => (vip ? _vipWhPage * VipWhPageSize : _clanWhPage * ClanWhPageSize) + index;
        var cell = new ItemSlotView(44) { Index = index, Name = "storage_cell_" + index };
        cell.RightClicked += _ => AskVaultTransfer(vip, false, Absolute(), -1);
        cell.Hovered += c => ShowItemTooltip(-1, c.Item);
        cell.Unhovered += _ => HideItemTooltip();
        cell.Wheeled += (_, step) => { if (vip) ChangeVipWhPage(step); else ChangeClanWhPage(step); };
        cell.DragOut = _ => new Godot.Collections.Dictionary { { "vaultFrom", Absolute() }, { "vipVault", vip } };
        cell.CanDrop = (_, data) => VaultAccepts(vip, false, data) || CanMoveVault(vip, Absolute(), data);
        cell.Dropped = (_, data) =>
        {
            var d = data.AsGodotDictionary();
            if (d.ContainsKey("invFrom")) AskVaultTransfer(vip, true, d["invFrom"].AsInt32(), Absolute());
            else if (CanMoveVault(vip, Absolute(), data)) MoveVault(vip, d["vaultFrom"].AsInt32(), Absolute());
        };
        return cell;
    }

    private bool CanMoveVault(bool vip, int target, Variant data)
    {
        if (!VaultAccepts(vip, true, data) || !vip && !Net.I.MyClan.CanInvite) return false;
        int source = data.AsGodotDictionary()["vaultFrom"].AsInt32(), pageSize = vip ? VipWhPageSize : ClanWhPageSize;
        var slots = vip ? _vipWh : _clanWh;
        return source != target && source / pageSize == target / pageSize && !slots[source].IsEmpty && slots[target].IsEmpty;
    }
    private void MoveVault(bool vip, int source, int target)
    {
        ResetVaultStatus(vip);
        if (vip)
        {
            _vipWhPending = new VipWhPending { Op = 4, VipIdx = source, To = target }; _vipWhInFlight = true;
            Net.I.SendVipWarehouseStore(_vipWh[source].ItemId, (byte)(source / VipWhPageSize), (byte)(source % VipWhPageSize), (byte)(target % VipWhPageSize));
        }
        else
        {
            _clanWhPending = new ClanWhPending { Op = 4, WhIdx = source, To = target }; _clanWhInFlight = true;
            Net.I.SendClanWhStore(_clanWh[source].ItemId, (byte)(source / ClanWhPageSize), (byte)(source % ClanWhPageSize), (byte)(target % ClanWhPageSize));
        }
    }

    private bool VaultAccepts(bool vip, bool bag, Variant data)
    {
        if (StorageTransferBusy || data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        if (!bag) return d.ContainsKey("invFrom") && InMainBag(d["invFrom"].AsInt32());
        return d.ContainsKey("vaultFrom") && d.ContainsKey("vipVault") && d["vipVault"].AsBool() == vip &&
            d["vaultFrom"].AsInt32() >= 0 && d["vaultFrom"].AsInt32() < (vip ? VipWhSlots : ClanWhSlots);
    }

    private void VaultStatus(bool vip, string text)
    {
        if (vip) _vipWhStatus.Text = text; else _clanWhStatus.Text = text;
    }

    private void ResetVaultStatus(bool vip) => VaultStatus(vip,
        (vip ? _vipWhPanel : _clanWhPanel).HasMeta("classic_storage") ? "" : "Right-click to store / withdraw");

    private void AskVaultTransfer(bool vip, bool deposit, int source, int target)
    {
        if (StorageTransferBusy || !(vip ? _vipWhShown : _clanWhShown)) return;
        var slots = vip ? _vipWh : _clanWh;
        if (vip && _vipWhExpirySec <= 0) { VaultStatus(true, "Vault rental expired. Renew it with a vault key."); return; }
        if (!vip && !_clanWhLoaded) { VaultStatus(false, "Waiting for clan storage."); return; }
        if (!vip && !deposit && !Net.I.MyClan.CanInvite) { VaultStatus(false, "Only the chief or vice-chief may withdraw."); return; }
        if (deposit ? !InMainBag(source) : source < 0 || source >= slots.Length) return;
        var slot = deposit ? Inv[source] : slots[source];
        if (slot.IsEmpty || slot.IsLinked) return;
        if (deposit && slot.ItemId is >= 900_000_001 and <= 999_999_999)
        { VaultStatus(vip, "This item is non-storable."); return; }
        void Transfer(long amount)
        {
            if (!(vip ? _vipWhShown : _clanWhShown) || !(deposit ? Inv[source] : slots[source]).Equals(slot))
            { VaultStatus(vip, "The item changed. Please select it again."); return; }
            SendVaultTransfer(vip, deposit, source, target, (int)amount);
        }
        if (IsStackable(slot.ItemId) && slot.Count > 1)
            (vip ? _vipWhAmount : _clanWhAmount).Open(ItemData.Icon(slot.ItemId),
                (deposit ? "Store " : "Take out ") + ItemData.DisplayName(slot.ItemId), $"Available {slot.Count:n0}",
                slot.Count, slot.Count, Transfer);
        else Transfer(1);
    }

    private void SendVaultTransfer(bool vip, bool deposit, int source, int target, int count)
    {
        if (StorageTransferBusy || count <= 0) return;
        if (!vip && (!deposit && !Net.I.MyClan.CanInvite || !_clanWhLoaded)) return;
        var slots = vip ? _vipWh : _clanWh;
        var slot = deposit ? Inv[source] : slots[source];
        if (slot.IsEmpty || count > slot.Count) return;
        int destination = target;
        bool merge;
        if (deposit)
        {
            if (destination < 0) destination = StorageDestination.Find(slots, slot.ItemId, count, IsStackable(slot.ItemId));
            if (destination < 0 || destination >= slots.Length || !StorageDestination.Fits(slots[destination], slot.ItemId, count, IsStackable(slot.ItemId)))
            { VaultStatus(vip, "No room in that storage slot."); return; }
            merge = !slots[destination].IsEmpty;
        }
        else
        {
            if (destination < 0) destination = BagDestination(slot.ItemId, count, out _);
            if (!FitsInBag(destination, slot.ItemId, count)) { VaultStatus(vip, "No room in that inventory slot."); return; }
            merge = !Inv[destination].IsEmpty;
        }
        int inventory = deposit ? source : destination, stored = deposit ? destination : source;
        byte op = (byte)(deposit ? 2 : 3);
        ResetVaultStatus(vip);
        if (vip)
        {
            _vipWhPending = new VipWhPending { Op = op, InvAbs = inventory, VipIdx = stored, Count = count, Merge = merge };
            _vipWhInFlight = true;
            if (deposit) Net.I.SendVipWarehouseInput(slot.ItemId, (byte)(stored / VipWhPageSize), (byte)(inventory - GridStart), (byte)(stored % VipWhPageSize), count);
            else Net.I.SendVipWarehouseOutput(slot.ItemId, (byte)(stored / VipWhPageSize), (byte)(stored % VipWhPageSize), (byte)(inventory - GridStart), count);
        }
        else
        {
            _clanWhPending = new ClanWhPending { Op = op, InvAbs = inventory, WhIdx = stored, Count = count, Merge = merge };
            _clanWhInFlight = true;
            if (deposit) Net.I.SendClanWhInput(slot.ItemId, (byte)(stored / ClanWhPageSize), (byte)(inventory - GridStart), (byte)(stored % ClanWhPageSize), count);
            else Net.I.SendClanWhOutput(slot.ItemId, (byte)(stored / ClanWhPageSize), (byte)(stored % ClanWhPageSize), (byte)(inventory - GridStart), count);
        }
    }

    private void AskClanGold(bool deposit)
    {
        if (_clanWhInFlight || !_clanWhLoaded || !deposit && !Net.I.MyClan.CanInvite) return;
        int maximum = System.Math.Min(deposit ? Sheet.Gold : _clanWhMoney,
            2_100_000_000 - (deposit ? _clanWhMoney : Sheet.Gold));
        if (maximum <= 0) return;
        _clanWhAmount.Open(null, deposit ? "Deposit gold" : "Withdraw gold", $"Available {maximum:n0}", maximum, maximum,
            n => { if (!_clanWhShown) return; _clanWhGoldInput.Text = n.ToString(); ClanWhGoldTransfer(deposit); });
    }
}
