using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class QuickslotSavesTests
{
    private const string EmptyName = "Empty Slot %d";

    [Fact]
    public void ThereAreThirtyTwoSetsNamedByNumberUntilSaved()
    {
        var saves = QuickslotSaves.Fresh(EmptyName);

        Assert.Equal("Empty Slot 1", saves[0].Name);
        Assert.Equal("Empty Slot 32", saves[QuickslotSaves.Sets - 1].Name);
        Assert.True(saves[5].IsEmpty);
        Assert.Equal(13, QuickslotSaves.Index(3, 1));
    }

    [Fact]
    public void ASavedSetRoundTripsThroughTheFile()
    {
        var saves = QuickslotSaves.Fresh(EmptyName);
        var set = saves[2].Copy();
        set.Name = "Hunting";
        set.Place(0, 105505, -1);
        set.Place(9, 490012, -1);
        saves.Store(2, set);

        var read = QuickslotSaves.Parse(saves.Write(), EmptyName);

        Assert.Equal("Hunting", read[2].Name);
        Assert.Equal(105505, read[2].Cells[0]);
        Assert.Equal(490012, read[2].Cells[9]);
        Assert.Equal("Empty Slot 4", read[3].Name);
    }

    [Fact]
    public void AnUnreadableFileGivesTheEmptySets() =>
        Assert.Equal("Empty Slot 1", QuickslotSaves.Parse("{not json", EmptyName)[0].Name);

    [Fact]
    public void DraggingBetweenCellsSwapsThemAndDraggingOutEmptiesOne()
    {
        var set = new QuickslotSet();
        set.Place(0, 105505, -1);
        set.Place(1, 105510, -1);

        Assert.True(set.Place(1, 105505, 0));
        Assert.Equal(new[] { 105510, 105505 }, set.Cells[..2]);
        Assert.True(set.Clear(0));
        Assert.False(set.Clear(0));
        set.Reset();
        Assert.True(set.IsEmpty);
    }

    [Fact]
    public void AStoredNameIsCutToThirtyTwoLetters()
    {
        var saves = QuickslotSaves.Fresh(EmptyName);
        saves.Store(0, new QuickslotSet { Name = new string('a', 40) });

        Assert.Equal(QuickslotSaves.MaxNameLength, saves[0].Name.Length);
    }

    [Fact]
    public void TheApplyQuestionNamesTheSetAndThePage() =>
        Assert.Equal("Will you apply Hunting in Quickslot 3?",
            QuickslotSaves.ApplyQuestion("Will you apply %s in Quickslot %d?", "Hunting", 3));

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(105505, true, true)]
    [InlineData(105505, false, false)]
    [InlineData(490012, false, true)]
    public void ItemSkillsAndEmptyCellsAlwaysApply(int skillId, bool learned, bool usable) =>
        Assert.Equal(usable, QuickslotSaves.Usable(skillId, _ => learned));
}
