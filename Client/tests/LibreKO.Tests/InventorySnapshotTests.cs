using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class InventorySnapshotTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void PartialBagTransferPreservesBothQuantitiesAfterReload(short stored)
    {
        var slots = new ItemSlot[InventoryConstants.InventoryTotal];
        slots[14] = new ItemSlot { ItemId = 810000000, Count = 100, Durability = 123, Flag = 2 };
        if (stored > 0) slots[60] = new ItemSlot { ItemId = 810000000, Count = stored, Durability = 123, Flag = 2 };
        ItemMove.ApplyConfirmed(slots, ItemMove.InventoryToMagicBag, 14, 60, 1, 25);
        var reloaded = new Inventory(); reloaded.Reset(slots);
        Assert.Equal(75, reloaded[14].Count); Assert.Equal(stored + 25, reloaded[60].Count);
        Assert.Equal(123, reloaded[60].Durability); Assert.Equal(2, reloaded[60].Flag);
    }

    [Theory]
    [InlineData(ItemMove.InventoryToSlot, 27, 13, 41, 13)]
    [InlineData(ItemMove.SlotToInventory, 13, 27, 13, 41)]
    [InlineData(ItemMove.InventoryToInventory, 0, 27, 14, 41)]
    [InlineData(ItemMove.SlotToSlot, 0, 13, 0, 13)]
    [InlineData(ItemMove.InventoryToCospre, 27, 14, 41, 56)]
    [InlineData(ItemMove.CospreToInventory, 14, 27, 56, 41)]
    [InlineData(ItemMove.InventoryToBagSlot, 27, 2, 41, 59)]
    [InlineData(ItemMove.BagSlotToInventory, 2, 27, 59, 41)]
    [InlineData(ItemMove.InventoryToMagicBag, 27, 35, 41, 95)]
    [InlineData(ItemMove.MagicBagToInventory, 35, 27, 95, 41)]
    [InlineData(ItemMove.MagicBagToMagicBag, 0, 35, 60, 95)]
    public void EveryConfirmedMoveSurvivesAZoneReload(byte direction, byte source, byte destination, int expectedFrom, int expectedTo)
    {
        Assert.True(ItemMove.TryResolveSlots(direction, source, destination, out int from, out int to));
        Assert.Equal(expectedFrom, from);
        Assert.Equal(expectedTo, to);
        var cached = new ItemSlot[InventoryConstants.InventoryTotal];
        var item = new ItemSlot { ItemId = 700011001, Count = 1, Durability = 100, UniqueId = 42 };
        cached[from] = item;
        ItemMove.ApplyConfirmed(cached, direction, from, to, 0);
        var newWorld = new Inventory();
        newWorld.Reset(cached);
        Assert.True(newWorld[from].IsEmpty);
        Assert.Equal(item, newWorld[to]);
    }

    [Theory]
    [InlineData(ItemMove.InventoryToInventory, 0, 1)]
    [InlineData(ItemMove.InventoryToMagicBag, 0, 35)]
    [InlineData(ItemMove.MagicBagToInventory, 35, 0)]
    [InlineData(ItemMove.MagicBagToMagicBag, 0, 35)]
    public void StackMergesSurviveAZoneReload(byte direction, byte source, byte destination)
    {
        Assert.True(ItemMove.TryResolveSlots(direction, source, destination, out int from, out int to));
        var cached = new ItemSlot[InventoryConstants.InventoryTotal];
        cached[from] = new ItemSlot { ItemId = 810000000, Count = 3 };
        cached[to] = new ItemSlot { ItemId = 810000000, Count = 7 };
        ItemMove.ApplyConfirmed(cached, direction, from, to, 1);
        var newWorld = new Inventory();
        newWorld.Reset(cached);
        Assert.True(newWorld[from].IsEmpty);
        Assert.Equal(10, newWorld[to].Count);
    }

    [Theory]
    [InlineData(ItemMove.InventoryToCospre, 28, 0)]
    [InlineData(ItemMove.InventoryToCospre, 0, 15)]
    [InlineData(ItemMove.InventoryToBagSlot, 0, 3)]
    [InlineData(ItemMove.MagicBagToInventory, 36, 0)]
    [InlineData(ItemMove.InventoryToPet, 0, 0)]
    [InlineData(ItemMove.InventoryToZone, 0, 0)]
    public void InvalidAndExternalSlotsDoNotModifyTheZoneSnapshot(byte direction, byte source, byte destination)
    {
        Assert.False(ItemMove.TryResolveSlots(direction, source, destination, out int from, out int to));
        Assert.Equal(-1, from);
        Assert.Equal(-1, to);
    }
}
