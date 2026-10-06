using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public sealed class ClassicChatFormatTests
{
    [Theory]
    [InlineData(3, "party", "5fd95f")]
    [InlineData(5, "shout", "ff9a3c")]
    [InlineData(6, "clan", "46d3c0")]
    [InlineData(15, "alliance", "6fb7ff")]
    public void RetainsOriginalChannelColors(byte type, string tag, string color)
    {
        Assert.Equal((tag, color), ClassicChatFormat.Channel(type));
    }

    [Theory]
    [InlineData(1, false, "e06666")]
    [InlineData(2, false, "6fa8ff")]
    [InlineData(1, true, "ffd24a")]
    public void RetainsNationAndGmNameColors(int nation, bool gm, string color)
    {
        Assert.Contains($"[color=#{color}]Canbo[/color]", ClassicChatFormat.Line(1, "Canbo", nation, gm, "Hello"));
    }

    [Fact]
    public void TreatsPlayerMarkupAsText()
    {
        string line=ClassicChatFormat.Line(1, "Canbo", 1, false, "[b]Hello[/b]");
        Assert.DoesNotContain("[b]", line);
        Assert.Contains("Hello", line);
    }
}
