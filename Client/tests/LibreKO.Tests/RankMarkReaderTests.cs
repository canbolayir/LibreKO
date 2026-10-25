using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class RankMarkReaderTests
{
    private sealed class NoConnection : IPacketSink
    {
        public bool Connected => false;

        public void Send(Packet packet) { }
    }

    [Fact]
    public void OpcodeC5IsTheOwnMarkListInTheUserCardBlocks()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new NoConnection(), session, new LoginState()), () => 0);
        int raised = 0;
        session.Self.RankMarksEvent += () => raised++;
        var p = new Packet(GameOpcodes.GS_RANK_MARKS);
        p.WriteByte(2);
        p.WriteUShort(0);
        p.WriteShort(0);
        p.WriteShort(0);
        p.WriteShort(3);
        p.WriteUShort(UserInfoCard.DroppedMarkType);
        p.WriteShort(1);
        p.WriteShort(0);
        p.WriteShort(0);
        p.WriteByte(1);
        p.WriteUShort(0);
        p.WriteUShort(1);
        p.ResetOffset();

        dispatcher.Dispatch(p);

        Assert.Equal(1, raised);
        Assert.Equal(new[] { new RankMark(0, 0, 0, 3), new RankMark(UserInfoCard.SpecialMarkFirst, 0, 0, 1) }, session.Self.RankMarks);
        Assert.Equal(0xC5, (int)GameOpcodes.GS_RANK_MARKS);
    }
}
