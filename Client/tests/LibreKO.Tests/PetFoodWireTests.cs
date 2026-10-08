using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class PetFoodWireTests
{
    private static Packet Food(byte result = 1, byte slot = 3, int item = 810684000, short count = 2, short increase = 1500)
    {
        var packet = new Packet((byte)GameOpcodes.GS_PET);
        packet.WriteByte(result); packet.WriteByte(slot); packet.WriteInt(item);
        if (result == 1) { packet.WriteShort(count); packet.WriteShort(0); packet.WriteInt(0); packet.WriteShort(increase); }
        packet.ResetOffset(); return packet;
    }

    [Fact]
    public void EveryTruncatedSuccessIsIgnoredInsteadOfBecomingARefusal()
    {
        var complete = Food().GetData();
        for (int length = 0; length < complete.Length; length++)
        {
            var prefix = new Packet((byte)GameOpcodes.GS_PET); prefix.WriteBytes(complete[..length]);
            Assert.False(PetWire.TryReadFood(prefix, out _));
        }
    }

    [Fact]
    public void AuthoritativeFoodSuccessAndRefusalHaveDistinctRecords()
    {
        Assert.True(PetWire.TryReadFood(Food(), out var success));
        Assert.Equal(new PetFoodReply(true, 3, 810684000, 2, 1500), success);
        Assert.True(PetWire.TryReadFood(Food(result: 0), out var refusal));
        Assert.Equal(new PetFoodReply(false, 3, 810684000, 0, 0), refusal);
    }

    [Theory]
    [InlineData(2, 3, 810684000, 2, 1500)]
    [InlineData(1, InventoryConstants.HaveMax, 810684000, 2, 1500)]
    [InlineData(1, 3, 0, 2, 1500)]
    [InlineData(1, 3, 810684000, -1, 1500)]
    [InlineData(1, 3, 810684000, 2, -1)]
    [InlineData(1, 3, 810684000, 2, PetSheet.MaxSatisfaction + 1)]
    public void InvalidFoodRepliesCannotReachInventoryOrStatusUpdates(byte result, byte slot, int item, short count, short increase)
        => Assert.False(PetWire.TryReadFood(Food(result, slot, item, count, increase), out _));
}
