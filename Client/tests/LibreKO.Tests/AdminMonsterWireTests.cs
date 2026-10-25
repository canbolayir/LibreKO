using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class AdminMonsterWireTests
{
    private const int WormId = 750;
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
    public void TheMonsterSheetCarriesFieldsDropsAndEventRates()
    {
        var (dispatcher, session) = Readers();
        AdminMonsterSheet? sheet = null;
        session.Admin.AdminMonsterSheetEvent += s => sheet = s;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.Monster, p =>
        {
            p.WriteByte(1);
            p.WriteInt(WormId);
            p.WriteSByteString("Worm");
            p.WriteByte(0);
            p.WriteInt(1234);
            p.WriteInt(3);
            p.WriteByte(1);
            p.WriteByte(9);
            p.WriteSByteString("Gold");
            p.WriteInt(1000);
            p.WriteInt(0);
            p.WriteInt(int.MaxValue);
            p.WriteShort(40);
            p.WriteByte(1);
            p.WriteUShort(2);
            p.WriteByte(7);
            for (int slot = 0; slot < 7; slot++)
            {
                p.WriteInt(slot == 0 ? Ruby : 0);
                p.WriteShort(slot == 0 ? (short)2500 : (short)0);
                p.WriteSByteString(slot == 0 ? "Ruby" : "");
            }
            p.WriteByte(50);
            p.WriteByte(25);
        }));

        Assert.NotNull(sheet);
        Assert.True(sheet.IsMonster);
        Assert.Equal(WormId, sheet.ProtoId);
        Assert.Equal("Worm", sheet.Name);
        Assert.Equal(1234, sheet.SpawnRow);
        Assert.Equal(3, sheet.Alive);
        Assert.Equal(new AdminMonsterField(9, "Gold", 1000, 0, int.MaxValue), Assert.Single(sheet.Fields));
        Assert.Equal(40, sheet.DropRow);
        Assert.True(sheet.DropRowExists);
        Assert.Equal(2, sheet.DropRowSharedBy);
        Assert.Equal(7, sheet.Drops.Count);
        Assert.Equal(new AdminDropSlot(Ruby, 2500, "Ruby"), sheet.Drops[0]);
        Assert.Equal(50, sheet.DropEventPercent);
        Assert.Equal(25, sheet.CoinEventPercent);
    }

    [Fact]
    public void AnItemGroupIsItsItems()
    {
        var (dispatcher, session) = Readers();
        AdminItemGroup? group = null;
        session.Admin.AdminItemGroupEvent += g => group = g;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.ItemGroup, p =>
        {
            p.WriteInt(9001);
            p.WriteByte(1);
            p.WriteUShort(2);
            p.WriteInt(Ruby);
            p.WriteInt(Ruby + 10000);
        }));

        Assert.NotNull(group);
        Assert.Equal(9001, group.Group);
        Assert.True(group.Exists);
        Assert.Equal(new[] { Ruby, Ruby + 10000 }, group.Items);
    }

    [Fact]
    public void TheSimulationCountsEachItem()
    {
        var (dispatcher, session) = Readers();
        AdminDropSimulation? result = null;
        session.Admin.AdminDropSimulationEvent += r => result = r;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.DropSimulation, p =>
        {
            p.WriteByte(1);
            p.WriteInt(WormId);
            p.WriteInt(200);
            p.WriteLong(170_000);
            p.WriteInt(700);
            p.WriteInt(1000);
            p.WriteInt(12);
            p.WriteUShort(1);
            p.WriteInt(Ruby);
            p.WriteInt(188);
        }));

        Assert.NotNull(result);
        Assert.Equal(200, result.Rolls);
        Assert.Equal(170_000, result.CoinTotal);
        Assert.Equal(700, result.CoinMin);
        Assert.Equal(1000, result.CoinMax);
        Assert.Equal(12, result.ItemlessKills);
        Assert.Equal((Ruby, 188), Assert.Single(result.Items));
    }
}
