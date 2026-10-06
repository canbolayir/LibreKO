using LibreKO.Common.Domain.Services;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PresetPlanTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void EveryClassTierMatchesTheActualServerRedistributionBase(int nationClassPrefix)
    {
        for (int familyAndTier = 1; familyAndTier <= 15; familyAndTier++)
        {
            int cls = nationClassPrefix + familyAndTier;
            var client = StarterStats.BaseForClass(cls);
            var server = ProgressionTable.BaseStatsForClass(cls);
            Assert.Equal(new[] { (int)server.Strength, server.Stamina, server.Dexterity, server.Intelligence, server.Magic },
                Enumerable.Range(0, CharacterSheet.StatCount).Select(client.StatAtRow));
        }
    }

    [Theory]
    [InlineData(Nations.Karus)]
    [InlineData(Nations.ElMorad)]
    public void EveryAllowedRaceCreationRollKeepsItsRedistributionBase(int nation)
    {
        foreach (int race in StarterStats.RacesFor(nation))
            foreach (int cls in StarterStats.ClassesFor(race))
                Assert.Equal(StarterStats.For(race, cls), StarterStats.BaseForClass(cls));
    }

    [Fact]
    public void ExistingSavedAllocationsDisplayAndSendTotalsWithoutChangingTheirMeaning()
    {
        var plan = new PresetPlan();
        int[] stored = [185, 55, 12, 0, 0];
        plan.SetStats(stored);
        Assert.Equal(new[] { 250, 120, 72, 50, 50 }, Enumerable.Range(0, 5).Select(i => plan.StatValue(206, i)));
        Assert.True(plan.TryStatValues(206, 302, out var request, out int remaining));
        Assert.Equal(new[] { 250, 120, 72, 50, 50 }, request);
        Assert.Equal(50, remaining);
        Assert.Equal(stored, plan.Stats);
    }

    [Fact]
    public void EmptyPlanMeansClassBaseWithAllPointsRemaining()
    {
        var plan = new PresetPlan();
        Assert.True(plan.TryStatValues(210, 302, out var request, out int remaining));
        Assert.Equal(new[] { 50, 50, 70, 70, 50 }, request);
        Assert.Equal(302, remaining);
    }

    [Fact]
    public void PlanningBeforeRedistributionCanReach255UsingTheActualBase()
    {
        var plan = new PresetPlan();
        Assert.Equal(205, plan.StatBudget(212, 0, 302));
        plan.Stats[0] = 205;
        Assert.Equal(255, plan.StatValue(212, 0));
        Assert.True(plan.TryStatValues(212, 302, out _, out int remaining));
        Assert.Equal(97, remaining);
        plan.Stats[0]++;
        Assert.False(plan.TryStatValues(212, 302, out _, out _));
    }

    [Fact]
    public void OtherStatsShareTheSameAvailablePool()
    {
        var plan = new PresetPlan();
        plan.Stats[0] = 190;
        plan.Stats[1] = 100;
        Assert.Equal(12, plan.StatBudget(206, 2, 302));
        plan.Stats[2] = 13;
        Assert.False(plan.TryStatValues(206, 302, out _, out _));
    }

    [Fact]
    public void MatchingTotalDoesNotAllowTheWrongClassBaseToBeApplied()
    {
        var plan = new PresetPlan();
        var sheet = new CharacterSheet();
        sheet.SeedStats(70, 60, 60, 50, 50, 302);
        Assert.True(sheet.AtBaseStats);
        Assert.False(plan.IsRedistributed(206, sheet));
        sheet.SeedStats(65, 65, 60, 50, 50, 302);
        Assert.True(plan.IsRedistributed(206, sheet));
    }

    [Theory]
    [InlineData(-1, 302)]
    [InlineData(191, 302)]
    [InlineData(20, 10)]
    [InlineData(int.MaxValue, 302)]
    public void InvalidStoredAllocationsCannotProduceAStatRequest(int strengthAllocation, int points)
    {
        var plan = new PresetPlan();
        plan.Stats[0] = strengthAllocation;
        Assert.False(plan.TryStatValues(106, points, out _, out _));
    }
}
