using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class KnightRoyaleTests
{
    [Theory]
    [InlineData(20051, 1)]
    [InlineData(20055, 5)]
    [InlineData(20056, 0)]
    public void GasRegionsMapToAreasOutsideIn(int region, int area) =>
        Assert.Equal(area, KnightRoyale.AreaOf(region));

    [Fact]
    public void AReleasedAreaWarnsOfPoisonAndALaterOneShowsItsNumber()
    {
        Assert.Equal(KnightRoyale.PoisonAreaText, KnightRoyale.AreaCaption(2, 2));
        Assert.Equal(KnightRoyale.AreaText, KnightRoyale.AreaCaption(3, 2));
    }

    [Fact]
    public void TheClientAddsOneGemToTheKillsAndSumsTheTotal()
    {
        var reward = new KnightRoyale.Reward(20, 3, 4, 10);
        Assert.Equal(5, reward.KillGems);
        Assert.Equal(15, reward.TotalGems);
        var lines = KnightRoyale.RewardLines(reward, (_, fallback) => fallback);
        Assert.Equal("You have received 15 Soul Gems.", lines[4]);
    }

    [Fact]
    public void SignUpOpenCarriesCountSecondsAndMode()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte((byte)KnightRoyaleSignUpResult.Open);
        p.WriteByte(12);
        p.WriteUShort(300);
        p.WriteByte(KnightRoyale.DuoMode);

        var signUp = KnightRoyaleWire.ReadSignUp(p);

        Assert.Equal(new RoyaleSignUp(KnightRoyaleSignUpResult.Open, 12, 300, KnightRoyale.DuoMode), signUp);
        Assert.Equal(KnightRoyale.DuoTitleText, KnightRoyale.TitleFor(signUp.Mode));
    }

    [Fact]
    public void RefusalsCarryOnlyTheirResult()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte((byte)KnightRoyaleSignUpResult.Level);

        var signUp = KnightRoyaleWire.ReadSignUp(p);

        Assert.Equal(0, signUp.Participants);
        Assert.Equal(KnightRoyale.LevelText, KnightRoyale.SignUpText(signUp.Result));
    }

    [Fact]
    public void StatsAndItemsReadTheirFields()
    {
        var stats = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        stats.WriteInt(9001);
        stats.WriteByte(3);
        stats.WriteShort(400);
        stats.WriteShort(120);
        stats.WriteShort(900);
        stats.WriteShort(850);
        stats.WriteShort(300);
        stats.WriteShort(280);
        Assert.Equal(new RoyaleStats(9001, 3, 400, 120, 900, 850, 300, 280), KnightRoyaleWire.ReadStats(stats));

        var item = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        item.WriteByte(2);
        item.WriteInt(930664000);
        item.WriteShort(3);
        item.WriteShort(1);
        item.WriteByte(0);
        Assert.Equal(new RoyaleItem(2, 930664000, 3, 1, 0), KnightRoyaleWire.ReadItem(item));
    }

    [Fact]
    public void ALootFailureStopsAfterItsCode()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteInt(77);
        p.WriteUShort(KnightRoyaleWire.LootFailed);

        Assert.Equal(new RoyaleLoot(77, KnightRoyaleWire.LootFailed, 0, 0), KnightRoyaleWire.ReadLoot(p));
    }

    [Fact]
    public void TheGasCountdownStopsAfterTheLastArea()
    {
        Assert.False(KnightRoyale.GasEnds(1));
        Assert.False(KnightRoyale.GasEnds(5));
        Assert.True(KnightRoyale.GasEnds(6));
        Assert.Equal("04:05", KnightRoyale.Clock(245));
    }

    private const int Greatsword = 930671000;
    private const int MythrilIngot = 978010000;
    private const int MithrilSword = 978008000;
    private const short MithrilSwordRow = 485;

    private sealed class RecordingSink : IPacketSink
    {
        public List<Packet> Sent { get; } = [];

        public bool Connected => true;

        public void Send(Packet packet) => Sent.Add(packet);
    }

    [Fact]
    public void ACraftSendsTheCombineBodyWithoutAnNpc()
    {
        var sink = new RecordingSink();
        var ordered = ItemCombine.WireOrder([new CombineEntry(MythrilIngot, 1, 4), new CombineEntry(Greatsword, 1, 2)]);

        new KnightRoyaleRequests(sink).RoyaleCraft(0, 0, ordered);

        var packet = Assert.Single(sink.Sent);
        Assert.Equal((byte)GameOpcodes.GS_KNIGHT_ROYALE, packet.GetOpcode());
        packet.ResetOffset();
        Assert.Equal((byte)KnightRoyaleSubOpcode.Combine, packet.ReadByte());
        Assert.Equal(0, packet.ReadInt());
        Assert.Equal((byte)0, packet.ReadByte());
        Assert.Equal((byte)2, packet.ReadByte());
        Assert.Equal((byte)2, packet.ReadByte());
        Assert.Equal((byte)4, packet.ReadByte());
        Assert.Equal("930671000001978010000001", packet.ReadSByteString());
        Assert.Equal(0, packet.RemainingBytes);
    }

    [Fact]
    public void ACraftReplyReachesTheCombineWindow()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new RecordingSink(), session, new LoginState()), () => 0);
        CombineReply? reply = null;
        session.ItemCombine.ItemCombineReplyEvent += r => reply = r;

        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte((byte)KnightRoyaleSubOpcode.Combine);
        p.WriteByte(ItemCombine.Succeeded);
        p.WriteShort(MithrilSwordRow);
        p.WriteInt(MithrilSword);
        p.WriteByte(3);
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.Equal(new CombineReply(ItemCombine.Succeeded, MithrilSwordRow, 3), reply);
    }

    [Fact]
    public void AFailedCraftCarriesOnlyItsRow()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte(ItemCombine.Failed);
        p.WriteShort(MithrilSwordRow);
        p.ResetOffset();

        Assert.Equal(new CombineReply(ItemCombine.Failed, MithrilSwordRow, 0), ItemCombineWire.ReadReply(p));
        Assert.Equal(0, p.RemainingBytes);
    }
}
