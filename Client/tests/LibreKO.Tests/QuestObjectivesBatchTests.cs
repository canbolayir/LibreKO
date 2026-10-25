using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class QuestObjectivesBatchTests
{
    private static Packet Batch(string hex)
    {
        var packet = new Packet(GameOpcodes.GS_QUEST);
        packet.WriteBytes(Convert.FromHexString(hex));
        Assert.Equal((byte)QuestSubOpcode.ObjectivesBatch, packet.ReadByte());
        return packet;
    }

    [Fact]
    public void EveryQuestInTheLoginBatchIsRead()
    {
        var all = QuestObjectives.ReadBatch(Batch("120200" + "3E0000010500027427000075270000" + "3F00010101000174270000"));

        Assert.Equal(2, all.Length);
        Assert.Equal(62, all[0].QuestId);
        Assert.Equal(new[] { 10100, 10101 }, all[0].Groups[0].Monsters);
        Assert.Equal(5, all[0].Groups[0].Count);
        Assert.Equal(63, all[1].QuestId);
        Assert.True(all[1].AnyWillDo);
    }

    [Theory]
    [InlineData("120000")]
    [InlineData("1201003E000001050002742700007527000000")]
    public void AnEmptyOrOverlongBatchIsRefused(string hex) =>
        Assert.Throws<InvalidDataException>(() => QuestObjectives.ReadBatch(Batch(hex)));
}
