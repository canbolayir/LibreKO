using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class AuctionTransactionsTests
{
    private static AuctionLot Lot(int slot = 1) => new(slot, 1310610106, 1, 1_000_000, 1_000_000, "Rikka");
    private static AuctionBidRow Row(byte slot = 1) => new(1, 2, slot, 1310610106, 1, 42, 0, 2_000_000, SpecialAuction.Outbid);

    [Fact]
    public void BidReplyKeepsSubmittedLotAmountAndAuctionDay()
    {
        var pending = new AuctionTransactions(); var lot = Lot();
        Assert.True(pending.BeginBid(lot, 1_013_000_000, 2, 7));
        Assert.False(pending.BeginBid(Lot(3), 999_000_000, 3, 8));
        var submitted = pending.CompleteBid()!;
        Assert.Equal(lot, submitted.Lot); Assert.Equal(1_013_000_000, submitted.Total);
        Assert.Equal(2, submitted.Group); Assert.Equal(7, submitted.Day);
        Assert.False(pending.Busy); Assert.Null(pending.CompleteBid());
    }
    [Fact]
    public void BidAndRowRequestsCannotOverwriteEachOther()
    {
        var pending = new AuctionTransactions();
        Assert.True(pending.BeginBid(Lot(), 2_000_000, 1, 2));
        Assert.False(pending.BeginRow(Row(), false)); Assert.Null(pending.CompleteRow(false)); Assert.True(pending.Busy);
        pending.CompleteBid(); Assert.True(pending.BeginRow(Row(), false));
        Assert.False(pending.BeginBid(Lot(), 2_000_000, 1, 2));
        Assert.Null(pending.CompleteBid()); Assert.True(pending.Busy);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongRowReplyDoesNotClearOrChangePendingRequest(bool claim)
    {
        var pending = new AuctionTransactions(); var row = Row();
        Assert.True(pending.BeginRow(row, claim)); Assert.False(pending.BeginRow(Row(4), claim));
        Assert.Null(pending.CompleteRow(!claim)); Assert.True(pending.Busy);
        Assert.Equal(row, pending.CompleteRow(claim)!.Row);
        Assert.False(pending.Busy); Assert.Null(pending.CompleteRow(claim));
    }
}
