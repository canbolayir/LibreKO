using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public sealed class BagStackTransferTests
{
    private const int Bag = InventoryConstants.MagicBagStart;
    private const int Grid = InventoryConstants.InventoryStart;
    private static Inventory Seed()
    {
        var inventory = new Inventory();
        inventory.EnsureLength(InventoryConstants.InventoryTotal);
        inventory[Bag] = new ItemSlot { ItemId=123, Count=100, Flag=1, Durability=90 };
        inventory[Grid+1] = new ItemSlot { ItemId=123, Count=200, Flag=1, Durability=90 };
        return inventory;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    public void EmptyDropTargetStillFillsExistingStack(int amount)
    {
        var inventory = Seed();
        Assert.Equal(Grid+1, inventory.FirstStackOrFreeGridSlot(inventory[Bag],1));
        var plan = inventory.PlanBagToGrid(Bag,Grid,amount,1);
        Assert.Equal(new[] {(Grid+1,amount)},plan);
        foreach(var step in plan)
        {
            var snapshot=Enumerable.Range(0,inventory.Length).Select(i=>inventory[i]).ToArray();
            ItemMove.ApplyConfirmed(snapshot,ItemMove.MagicBagToInventory,Bag,step.Slot,1,step.Count);
            inventory.Reset(snapshot);
        }
        Assert.True(inventory[Grid].IsEmpty);
        Assert.Equal(200+amount,inventory[Grid+1].Count);
        Assert.Equal(amount==100?0:100-amount,inventory[Bag].Count);
    }

    [Fact]
    public void OverflowFillsExistingStacksBeforePreferredEmptySlot()
    {
        var inventory=Seed();
        var almostFull=inventory[Grid+1];almostFull.Count=9990;inventory[Grid+1]=almostFull;
        inventory[Grid+2]=almostFull;
        Assert.Equal(new[] {(Grid+1,9),(Grid+2,9),(Grid,82)},inventory.PlanBagToGrid(Bag,Grid,100,1));
    }

    [Fact]
    public void FullInventoryStillAllowsStackMerge()
    {
        var inventory=Seed();
        for(int i=Grid;i<Grid+Inventory.GridCount;i++)
            if(i!=Grid+1) inventory[i]=new ItemSlot {ItemId=456,Count=1};
        Assert.Equal(new[] {(Grid+1,25)},inventory.PlanBagToGrid(Bag,Grid+1,25,1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidAmountCannotProduceMove(int amount) => Assert.Empty(Seed().PlanBagToGrid(Bag,Grid,amount,1));

    [Fact]
    public void InsufficientCapacityProducesNoPartialPlan()
    {
        var inventory=Seed();
        for(int i=Grid;i<Grid+Inventory.GridCount;i++) inventory[i]=new ItemSlot {ItemId=456,Count=1};
        inventory[Grid+1]=new ItemSlot {ItemId=123,Count=9990,Flag=1};
        Assert.Empty(inventory.PlanBagToGrid(Bag,Grid+1,100,1));
    }

    [Theory]
    [InlineData(2,0)]
    [InlineData(1,7)]
    public void DifferentFlagsAndLinkedItemsDoNotMerge(byte flag,int uniqueId)
    {
        var inventory=Seed();var target=inventory[Grid+1];target.Flag=flag;target.UniqueId=uniqueId;inventory[Grid+1]=target;
        Assert.Equal(new[] {(Grid,25)},inventory.PlanBagToGrid(Bag,Grid,25,1));
    }
}
