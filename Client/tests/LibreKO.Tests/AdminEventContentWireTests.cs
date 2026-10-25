using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class AdminEventContentWireTests
{
    private const int Ruby = 389010000;

    private sealed class NoConnection : IPacketSink
    {
        public bool Connected => false;
        public void Send(Packet packet) { }
    }

    private static (PacketDispatcher Dispatcher, GameSession Session) Readers()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new NoConnection(), session, new LoginState()), () => 0);
        return (dispatcher, session);
    }

    private static Packet Reply(AdminReplySubOpcode sub, Action<Packet> body)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)sub);
        body(p);
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void ALotteryArrivesWithItsRewards()
    {
        var (dispatcher, session) = Readers();
        AdminLottery? shown = null;
        session.Admin.AdminLotteryEvent += l => shown = l;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.Lottery, p =>
        {
            p.WriteInt(1);
            p.WriteSByteString("Moradon Draw");
            p.WriteInt(15);
            p.WriteInt(100);
            p.WriteInt(900000000);
            p.WriteInt(1000);
            p.WriteByte(1);
            p.WriteByte(0);
            p.WriteByte(1);
            p.WriteInt(1);
            p.WriteInt(Ruby);
            p.WriteInt(2);
        }));

        Assert.NotNull(shown);
        Assert.Equal("Moradon Draw", shown.Name);
        Assert.True(shown.AutoStart);
        Assert.False(shown.Running);
        Assert.Equal(new AdminLotteryReward(1, Ruby, 2), Assert.Single(shown.Rewards));
    }

    [Fact]
    public void ARaceArrivesWithItsTargetsAndRewards()
    {
        var (dispatcher, session) = Readers();
        AdminRace? shown = null;
        session.Admin.AdminRaceEvent += r => shown = r;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.Race, p =>
        {
            p.WriteInt(5);
            p.WriteSByteString("Worm Hunt");
            p.WriteByte(21);
            p.WriteByte(1);
            p.WriteByte(30);
            p.WriteInt(60);
            p.WriteInt(10);
            p.WriteByte(0);
            p.WriteByte(1);
            p.WriteByte(2);
            p.WriteByte((byte)AdminRaceTarget.Monster);
            p.WriteInt(750);
            p.WriteInt(20);
            p.WriteSByteString("Worm");
            p.WriteByte((byte)AdminRaceTarget.EnemyPlayer);
            p.WriteInt(0);
            p.WriteInt(2);
            p.WriteSByteString("enemy players");
            p.WriteByte(1);
            p.WriteInt(Ruby);
            p.WriteInt(1);
            p.WriteByte(50);
        }));

        Assert.NotNull(shown);
        Assert.Equal(21, shown.ZoneId);
        Assert.True(shown.Running);
        Assert.Equal(new AdminRaceObjective(AdminRaceTarget.Monster, 750, 20, "Worm"), shown.Objectives[0]);
        Assert.Equal(AdminRaceTarget.EnemyPlayer, shown.Objectives[1].Kind);
        Assert.Equal(new AdminRaceReward(Ruby, 1, 50), Assert.Single(shown.Rewards));
    }
}
