using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class AttackPresentationWireTests
{
    private const int AttackerId = 1201;
    private const int TargetId = 1202;
    private const byte Hit = 1;
    private const byte Chained = 1;
    private static readonly AttackPresentation Shown = new(3, 1.25f, 0.4f);

    private sealed class Capture : IPacketSink
    {
        public Packet? Last;
        public bool Connected => true;
        public void Send(Packet packet) => Last = packet;
    }

    private static (PacketDispatcher Dispatcher, GameSession Session) Readers()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new Capture(), session, new LoginState()), () => 0);
        return (dispatcher, session);
    }

    private static Packet Reply(AttackPresentation? shown)
    {
        var p = new Packet(GameOpcodes.GS_ATTACK);
        p.WriteByte((byte)AttackType.Melee);
        p.WriteByte(Hit);
        p.WriteInt(AttackerId);
        p.WriteInt(TargetId);
        p.WriteByte(Chained);
        shown?.Write(p);
        p.ResetOffset();
        return p;
    }

    private static AttackReply? Dispatch(Packet packet)
    {
        var (dispatcher, session) = Readers();
        AttackReply? reply = null;
        session.Combat.AttackEvent += r => reply = r;
        dispatcher.Dispatch(packet);
        return reply;
    }

    [Fact]
    public void AReplyWithTheTrailerCarriesTheShownSwing()
    {
        var reply = Dispatch(Reply(Shown));
        Assert.NotNull(reply);
        Assert.True(reply.Value.Chained);
        Assert.Equal(Shown, reply.Value.Shown);
    }

    [Fact]
    public void AReplyWithoutTheTrailerHasNoShownSwing()
    {
        var reply = Dispatch(Reply(null));
        Assert.NotNull(reply);
        Assert.True(reply.Value.Chained);
        Assert.Null(reply.Value.Shown);
    }

    [Fact]
    public void TheRequestEndsWithTheTrailerAfterTheRetailFields()
    {
        var sink = new Capture();
        var session = new GameSession();
        var requests = new GameRequests(sink, session, new LoginState());
        requests.Combat.Attack(TargetId, 120, chained: true, shown: Shown);
        var p = sink.Last!;
        p.ResetOffset();
        int retailBytes = p.RemainingBytes - AttackPresentation.Size;
        for (int i = 0; i < retailBytes; i++) p.ReadByte();
        Assert.Equal(Shown, AttackPresentation.Read(p));
    }
}
