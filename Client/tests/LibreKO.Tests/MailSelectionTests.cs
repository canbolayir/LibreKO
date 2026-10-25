using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class MailSelectionTests
{
    private static MailEntry Letter(int id, MailAttachmentState attachments = MailAttachmentState.None) =>
        new() { Id = id, Attachments = attachments };

    [Fact]
    public void ALetterWithUnclaimedAttachmentsCannotBeTicked()
    {
        var selection = new MailSelection();

        selection.Set(Letter(1), true);
        selection.Set(Letter(2, MailAttachmentState.Pending), true);

        Assert.True(selection.Has(1));
        Assert.False(selection.Has(2));
        Assert.Equal(1, selection.Count);
    }

    [Fact]
    public void SelectAllTicksEveryDeletableLetterShown()
    {
        var shown = new[] { Letter(1), Letter(2, MailAttachmentState.Pending), Letter(3, MailAttachmentState.Claimed) };
        var selection = new MailSelection();

        selection.SetAll(shown, true);

        Assert.True(selection.AllTicked(shown));
        Assert.Equal(2, selection.Count);
        selection.SetAll(shown, false);
        Assert.Equal(0, selection.Count);
    }

    [Fact]
    public void ARefreshedInboxDropsTicksForLettersThatAreGoneOrNowHoldParcels()
    {
        var selection = new MailSelection();
        selection.SetAll([Letter(1), Letter(2), Letter(3)], true);

        selection.KeepOnly([Letter(1), Letter(3, MailAttachmentState.Pending)]);

        Assert.True(selection.Has(1));
        Assert.False(selection.Has(2));
        Assert.False(selection.Has(3));
    }

    [Fact]
    public void TakingTheSelectionQueuesItAndClearsIt()
    {
        var selection = new MailSelection();
        selection.SetAll([Letter(9), Letter(4)], true);

        var queue = selection.TakeAll();

        Assert.Equal(new[] { 4, 9 }, queue);
        Assert.Equal(0, selection.Count);
    }
}
