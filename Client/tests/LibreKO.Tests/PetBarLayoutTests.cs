using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PetBarLayoutTests
{
    [Fact]
    public void WithNoSavedLayoutTheSkillsFillTheBarInIdOrder()
    {
        var layout = PetBarLayout.InIdOrder([301010, 301020]);

        Assert.Equal(301010, layout[0]);
        Assert.Equal(301020, layout[1]);
        Assert.Equal(0, layout[2]);
        Assert.False(layout.Saved);
    }

    [Fact]
    public void DroppingFromTheSkillListFillsTheCellAndDraggingBetweenCellsSwaps()
    {
        var layout = PetBarLayout.InIdOrder([301010, 301020]);

        layout.Place(9, 301030, fromSlot: -1);
        layout.Place(1, 301030, fromSlot: 9);

        Assert.Equal(301030, layout[1]);
        Assert.Equal(301020, layout[9]);
        Assert.True(layout.Saved);
    }

    [Fact]
    public void DraggingACellOffTheBarClearsIt()
    {
        var layout = PetBarLayout.InIdOrder([301010]);

        Assert.True(layout.Clear(0));
        Assert.False(layout.Clear(0));
        Assert.Equal(0, layout[0]);
    }

    [Fact]
    public void ALayoutSurvivesARoundTripAndDropsSkillsTheFamiliarDoesNotHave()
    {
        var layout = PetBarLayout.InIdOrder([301010, 302010]);
        layout.Place(15, 301020, fromSlot: -1);

        var read = PetBarLayout.Parse(layout.Write())!;
        read.KeepOnly(id => id != 302010);

        Assert.Equal(301010, read[0]);
        Assert.Equal(0, read[1]);
        Assert.Equal(301020, read[15]);
        Assert.True(read.Saved);
    }

    [Fact]
    public void AutoCastIsPerCellMovesWithTheSkillAndEndsWhenTheCellIsCleared()
    {
        var layout = PetBarLayout.InIdOrder([301001, 301002]);

        Assert.True(layout.ToggleAuto(0));
        Assert.False(layout.ToggleAuto(5));
        layout.Place(3, 301001, fromSlot: 0);

        Assert.True(layout.IsAuto(3));
        Assert.False(layout.IsAuto(0));
        layout.Place(3, 301002, fromSlot: -1);
        Assert.False(layout.IsAuto(3));
        Assert.True(layout.ToggleAuto(1));
        layout.Clear(1);
        Assert.False(layout.IsAuto(1));
    }

    [Fact]
    public void AutoCastFlagsAreSavedWithTheLayoutAndAnOldFileHasNone()
    {
        var layout = PetBarLayout.InIdOrder([301001, 301002]);
        layout.ToggleAuto(1);

        var read = PetBarLayout.Parse(layout.Write())!;
        var old = PetBarLayout.Parse("{\"slots\":[301001,301002]}")!;

        Assert.False(read.IsAuto(0));
        Assert.True(read.IsAuto(1));
        Assert.False(old.IsAuto(1));
    }

    [Fact]
    public void TheFirstFlaggedSkillThatIsReadyIsCast()
    {
        var layout = PetBarLayout.InIdOrder([301001, 301002, 301003]);
        layout.ToggleAuto(1);
        layout.ToggleAuto(2);

        Assert.Equal(301002, PetSkills.NextAutoCast(layout, _ => true));
        Assert.Equal(301003, PetSkills.NextAutoCast(layout, id => id != 301002));
        Assert.Equal(0, PetSkills.NextAutoCast(layout, id => id == 301001));
    }

    [Fact]
    public void TheAutoCastFrameLoopsTwentyFramesAtTwentyFivePerSecond()
    {
        Assert.Equal(0, PetSkills.AutoFrameAt(0));
        Assert.Equal(1, PetSkills.AutoFrameAt(0.05));
        Assert.Equal(19, PetSkills.AutoFrameAt(0.79));
        Assert.Equal(0, PetSkills.AutoFrameAt(0.81));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not json")]
    public void AMissingOrBrokenFileHasNoLayout(string? json) => Assert.Null(PetBarLayout.Parse(json));
}
