using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PartyLootLogTests
{
    private static readonly System.DateTime At = new(2026, 10, 23, 12, 0, 0);

    [Fact]
    public void SharesCountEachMembersItemsMostFirst()
    {
        var log = new PartyLootLog();
        log.AddItem(new PartyLootEntry(At, "Rikka", 379068000));
        log.AddItem(new PartyLootEntry(At, "Zeus", 389155000));
        log.AddItem(new PartyLootEntry(At, "Rikka", 389155000));
        log.AddItem(new PartyLootEntry(At, "Rikka", 379068000));

        Assert.Equal(new[] { new PartyLootShare("Rikka", 3, 75), new PartyLootShare("Zeus", 1, 25) }, log.Shares());
    }

    [Fact]
    public void OnlyNoahGainsAddUp()
    {
        var log = new PartyLootLog();
        log.AddNoah(1200);
        log.AddNoah(-50);
        log.AddNoah(300);

        Assert.Equal(1500, log.MyNoah);
    }

    [Fact]
    public void TheLogKeepsTheNewestEntries()
    {
        var log = new PartyLootLog();
        for (int i = 0; i < PartyLootLog.MaxEntries + 5; i++) log.AddItem(new PartyLootEntry(At, "Zeus", 100000000 + i));

        Assert.Equal(PartyLootLog.MaxEntries, log.Entries.Count);
        Assert.Equal(100000005, log.Entries[0].ItemId);
    }

    [Fact]
    public void ClearingForgetsDropsAndNoah()
    {
        var log = new PartyLootLog();
        int changes = 0;
        log.Changed += () => changes++;
        log.AddItem(new PartyLootEntry(At, "Zeus", 389155000));
        log.AddNoah(10);
        log.Clear();
        log.Clear();

        Assert.Empty(log.Entries);
        Assert.Equal(0, log.MyNoah);
        Assert.Equal(3, changes);
    }
}
