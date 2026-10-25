using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class MovieWireTests
{
    private sealed class NoConnection : IPacketSink
    {
        public bool Connected => false;
        public void Send(Packet packet) { }
    }

    private static int? Dispatch(Action<Packet> body)
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new NoConnection(), session, new LoginState()), () => 0);
        int? played = null;
        session.Movie.MovieEvent += id => played = id;
        var p = new Packet(GameOpcodes.GS_UTC_MOVIE);
        body(p);
        p.ResetOffset();
        dispatcher.Dispatch(p);
        return played;
    }

    [Fact]
    public void TheMovieIdFollowsFourIgnoredBytes() =>
        Assert.Equal(7, Dispatch(p =>
        {
            p.WriteByte(2);
            p.WriteByte(1);
            p.WriteInt(0);
            p.WriteInt(7);
        }));

    [Fact]
    public void AShortMoviePacketPlaysNothing() =>
        Assert.Null(Dispatch(p =>
        {
            p.WriteByte(2);
            p.WriteByte(1);
            p.WriteShort(1);
            p.WriteInt(7);
        }));
}
