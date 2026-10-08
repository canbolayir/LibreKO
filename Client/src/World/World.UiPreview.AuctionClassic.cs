using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    // Offline fixtures use the real auction controls and callbacks, without sending live transactions.
    internal CanvasLayer BuildAuctionClassicUiPreview()
    {
        BuildItemTooltip();
        var window = BuildSpecialAuctionUiPreview("classic-today");
        window.SetMeta("auction_pack_table_loaded", _auctionTable.Length > 0);
        int[] items = [AgilityNecklace, StrengthNecklace, ShadowPiece, ElfMetalEarrings, PreviewHpPotion, PreviewMpPotion, PreviewUpgradedWeapon, 379021000];
        var table = new List<AuctionScheduleLot>();
        for (int day = 1; day <= SpecialAuction.DaysPerGroup; day++)
            for (int slot = 0; slot < items.Length; slot++)
                table.Add(new AuctionScheduleLot(SpecialAuction.Row(1, day), slot, items[slot], slot == 4 ? 20 : 1, 1_000_000, 1_000_000, day > AuctionPreviewDay && slot == 3));
        _auctionTable = table.ToArray();
        OnAuctionToday(new AuctionToday(SpecialAuction.Bidding, 1, AuctionPreviewSeconds, AuctionPreviewDay,
            items.Select((item, slot) => new AuctionOffer(slot, item, slot == 1 ? 1_012_000_000 : slot == 2 ? 230_000_000 : 0, slot == 1 ? "Rikka" : slot == 2 ? "Zeus" : "")).ToArray()));
        SelectAuctionLot(1); _auctionMillions.Value = 13; _auctionCheckInput.Value = 1; RefreshAuctionTotal();
        _itemTipLayer.Reparent(_specialAuctionLayer, false);
        RemoveChild(_specialAuctionLayer); return _specialAuctionLayer;
    }
}
