using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class UpgradePreviewGateTests
{
    [Fact]
    public void OnlyOnePreviewIsSentUntilItsReplyArrives()
    {
        var gate = new UpgradePreviewGate();
        Assert.True(gate.Begin([1, 2], [0, 1]));
        Assert.False(gate.Begin([1, 3], [0, 2]));
        Assert.False(gate.Complete([1, 3], [0, 2]));
        Assert.True(gate.Begin([1, 3], [0, 2]));
        Assert.True(gate.Complete([1, 3], [0, 2]));
    }

    [Fact]
    public void EditingTheOriginOrClosingRejectsThePreviousReply()
    {
        var gate = new UpgradePreviewGate();
        int[] items = [1, 2], positions = [0, 1];
        gate.Begin(items, positions);
        items[0] = 3;
        Assert.False(gate.Complete(items, positions));
        gate.Begin(items, positions);
        Assert.False(gate.Complete([0, 0], [-1, -1]));
        Assert.False(gate.Complete(items, positions));
    }
}
