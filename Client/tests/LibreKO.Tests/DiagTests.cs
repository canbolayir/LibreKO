using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class DiagTests
{
    [Fact]
    public void APacketLineNamesTheOpcodeItsSubAndItsSize()
    {
        Assert.Equal("GS_EVENT (0x5F) sub 35, 16 bytes", Diag.PacketLine((byte)GameOpcodes.GS_EVENT, 35, 16));
        Assert.Equal("GS_WARP (0x1E), 9 bytes", Diag.PacketLine((byte)GameOpcodes.GS_WARP, -1, 9));
    }

    [Fact]
    public void AnUnknownOpcodeIsStillListed() =>
        Assert.StartsWith("? (0xFD)", Diag.PacketLine(0xFD, 3, 2));
}
