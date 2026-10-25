using LibreKO.Domain;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class QuestPrefsTests
{
    [Fact]
    public void TrackedAndOfferedQuestsSurviveARoundTrip()
    {
        var prefs = new QuestPrefs();
        prefs.Tracked.Add(301);
        prefs.Tracked.Add(77);
        prefs.Offered.Add(12);

        var read = QuestPrefs.Parse(prefs.Write());

        Assert.Equal(new[] { 77, 301 }, read.Tracked.Order());
        Assert.Equal(new[] { 12 }, read.Offered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void AMissingOrBrokenFileStartsEmpty(string? json)
    {
        var prefs = QuestPrefs.Parse(json);

        Assert.Empty(prefs.Tracked);
        Assert.Empty(prefs.Offered);
    }

    [Fact]
    public void OnlyANewlyAcceptedQuestCountsAsAccepted()
    {
        var book = new QuestBook();
        var accepted = new List<int>();
        book.Accepted += accepted.Add;

        book.ApplyState(5, QuestStates.Active);
        book.ApplyState(5, QuestStates.ReadyToTurnIn);
        book.ApplyState(5, QuestStates.Active);
        book.ApplyState(6, QuestStates.Available);

        Assert.Equal(new[] { 5 }, accepted);
    }
}
