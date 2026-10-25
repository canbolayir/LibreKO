using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class WhisperStackTests
{
    [Fact]
    public void WindowsStackUpwardFromTheChatWithTheActiveOneNearest()
    {
        var places = WhisperStack.Plan(
            [new("Rikka", true, 200, 30), new("Zeus", true, 200, 30)], bottom: 700, top: 0, gap: 4);

        Assert.Equal(new WhisperPlace("Rikka", true, false, 500), places[0]);
        Assert.Equal(new WhisperPlace("Zeus", true, false, 296), places[1]);
    }

    [Fact]
    public void WithoutRoomTheRestFoldAndThenHide()
    {
        var places = WhisperStack.Plan(
            [new("Active", true, 200, 30), new("Second", true, 200, 30), new("Third", true, 200, 30)], bottom: 260, top: 0, gap: 4);

        Assert.True(places[0].Open);
        Assert.False(places[1].Open);
        Assert.False(places[1].Hidden);
        Assert.Equal(26, places[1].Y);
        Assert.True(places[2].Hidden);
    }

    [Fact]
    public void AWindowThePlayerFoldedStaysFolded()
    {
        var places = WhisperStack.Plan([new("Folded", false, 200, 30)], bottom: 700, top: 0, gap: 4);

        Assert.Equal(new WhisperPlace("Folded", false, false, 670), places[0]);
    }
}
