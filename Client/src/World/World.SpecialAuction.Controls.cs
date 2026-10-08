using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LibreKO;

public partial class World
{
    private void NameAuctionControls()
    {
        foreach (var entry in new (Control Control, string Name)[]
        {
            (_auctionSelectedName, "selected_name"), (_auctionGold, "gold"), (_auctionChecks, "checks"),
            (_auctionTotal, "total"), (_auctionWords, "words"), (_auctionCurrent, "current"),
            (_auctionMinimum, "minimum"), (_auctionClock, "clock"), (_auctionStatus, "status"),
            (_auctionMillions, "millions"), (_auctionCheckInput, "check_input"), (_auctionSchedule, "schedule"),
            (_auctionBidList, "bid_list"), (_auctionWinList, "win_list"), (_auctionLog, "log"),
            (_auctionPlaceBid, "place_bid"), (_auctionRefresh, "refresh"),
        }) entry.Control.Name = "auction_" + entry.Name;
    }
    private static IEnumerable<Control> AuctionControls(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control control) yield return control;
            foreach (var descendant in AuctionControls(child)) yield return descendant;
        }
    }
    private void RefreshAuctionTransactions()
    {
        if (!IsInstanceValid(_auctionPlaceBid)) return;
        bool busy = _auctionTransactions.Busy;
        _auctionPlaceBid.Disabled = busy || SelectedAuctionLot == null;
        _auctionRefresh.Disabled = busy;
        _auctionMillions.Editable = _auctionCheckInput.Editable = !busy;
        foreach (var button in AuctionControls(_auctionBidList).Concat(AuctionControls(_auctionWinList)))
            if (button is Button action && action.HasMeta("auction_row_action")) action.Disabled = busy;
    }
}
