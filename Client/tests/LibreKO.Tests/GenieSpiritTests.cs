using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class GenieSpiritTests
{
    [Theory]
    [InlineData(new[] { 811062000, 810378000 }, 810378000)]
    [InlineData(new[] { 811062000 }, 811062000)]
    [InlineData(new[] { 900772000, 810305000 }, 900772000)]
    [InlineData(new int[0], GenieSpirit.NoSpirit)]
    public void TheFirstOwnedSpiritFollowsTheRetailOrder(int[] owned, int expected) =>
        Assert.Equal(expected, GenieSpirit.FirstOwned(owned.Contains));

    [Fact]
    public void TheSpiritRequestNamesTheItem()
    {
        var bytes = GenieRequests.SpiritPacket(811062000).GetBytes();

        Assert.Equal((byte)GameOpcodes.GS_GENIE_SYSTEM, bytes[0]);
        Assert.Equal((byte)GenieSystemSubOpcode.InfoRequest, bytes[1]);
        Assert.Equal((byte)GenieSystemCommand.UseSpiritPotion, bytes[2]);
        Assert.Equal(811062000, System.BitConverter.ToInt32(bytes, 3));
        Assert.Equal(7, bytes.Length);
    }

    [Theory]
    [InlineData(GenieSystemCommand.UseSpiritPotion, 1)]
    [InlineData(GenieSystemCommand.RemainingTime, 0)]
    public void OnlyTheSpiritReplySaysAPointWasUsed(GenieSystemCommand command, int used)
    {
        var genie = new GenieSession();
        var dispatcher = new PacketDispatcher();
        new GenieSystemReader(genie).Register(dispatcher);
        int spiritUsed = 0;
        int? minutes = null;
        genie.GenieSpiritUsedEvent += () => spiritUsed++;
        genie.GenieSystemState += (_, left) => minutes = left;

        var p = new Packet(GameOpcodes.GS_GENIE_SYSTEM);
        p.WriteByte((byte)GenieSystemSubOpcode.InfoRequest);
        p.WriteByte((byte)command);
        p.WriteUShort(120);
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.Equal(used, spiritUsed);
        Assert.Equal(120, minutes);
    }
}
