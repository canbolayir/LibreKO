using LibreKO.Network;
using LibreKO.Network.Requests;
using Xunit;

namespace LibreKO.Tests;

public class MailDeleteWireTests
{
    private sealed class Recorder : IPacketSink
    {
        public readonly List<Packet> Sent = new();
        public bool Connected => true;
        public void Send(Packet packet) => Sent.Add(packet);
    }

    [Theory]
    [InlineData(false, new byte[] { 4, 0x39, 0x30, 0, 0 })]
    [InlineData(true, new byte[] { 4, 0x39, 0x30, 0, 0, 1 })]
    public void ADeleteCarriesTheDiscardByteOnlyWhenAttachmentsAreGivenUp(bool discard, byte[] body)
    {
        var sink = new Recorder();

        new MailRequests(sink).Delete(12345, discard);

        var sent = Assert.Single(sink.Sent);
        Assert.Equal((byte)GameOpcodes.GS_MAIL, sent.GetOpcode());
        Assert.Equal(body, sent.GetData());
    }
}
