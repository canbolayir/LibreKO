using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class TempleJoinTests
{
    [Theory]
    [InlineData((byte)TempleJoinResult.AlreadyJoined, 17999)]
    [InlineData((byte)TempleJoinResult.NeedsMapOfChaos, 18801)]
    [InlineData((byte)TempleJoinResult.LadderPoints, 41005)]
    [InlineData((byte)TempleJoinResult.ApplyLadderPoints, 41005)]
    [InlineData((byte)TempleJoinResult.LevelTooLow, 44543)]
    [InlineData((byte)TempleJoinResult.DuplicateIp, 44422)]
    [InlineData((byte)TempleJoinResult.NotOpen, 44545)]
    [InlineData((byte)TempleJoinResult.Joined, 0)]
    [InlineData((byte)TempleJoinResult.LevelTooHigh, 0)]
    public void ResultCodesUseTheirTexts(byte result, int text) =>
        Assert.Equal(text, TempleJoin.TextId((TempleJoinResult)result));

    [Fact]
    public void BothJoinedAndAlreadyJoinedLeaveYouSignedUp()
    {
        Assert.True(TempleJoin.SignedUp(TempleJoinResult.Joined));
        Assert.True(TempleJoin.SignedUp(TempleJoinResult.AlreadyJoined));
        Assert.False(TempleJoin.SignedUp(TempleJoinResult.NotOpen));
    }

    [Theory]
    [InlineData(TempleEventType.BorderDefenseWar, "Karus 2  ·  El Morad 3")]
    [InlineData(TempleEventType.JuraidMountain, "Karus 2  ·  El Morad 3")]
    [InlineData(TempleEventType.Chaos, "Signed up: 5")]
    [InlineData(TempleEventType.UnderTheCastle, "Signed up: 5")]
    [InlineData(TempleEventType.ForgottenTemple, "Signed up: 5")]
    public void OnlyTheBorderWarBoardsCountEachNation(TempleEventType type, string expected) =>
        Assert.Equal(expected, TempleJoin.CountLine(type, 2, 3));

    [Theory]
    [InlineData(754, "12:34")]
    [InlineData(3600 + 65, "01:05")]
    [InlineData(0, "00:00")]
    public void TheBoardClockShowsMinutesAndSecondsWithinTheHour(int seconds, string expected) =>
        Assert.Equal(expected, TempleJoin.Clock(seconds));

    [Theory]
    [InlineData(24, TempleEventType.Chaos)]
    [InlineData(2, TempleEventType.BorderDefenseWar)]
    [InlineData(7, TempleEventType.BorderDefenseWar)]
    [InlineData(10, TempleEventType.BorderDefenseWar)]
    [InlineData(100, TempleEventType.JuraidMountain)]
    [InlineData(101, TempleEventType.UnderTheCastle)]
    [InlineData(102, TempleEventType.ForgottenTemple)]
    [InlineData(47, TempleEventType.None)]
    public void TheWireTypeNamesTheEvent(int indunType, TempleEventType type) =>
        Assert.Equal(type, TempleJoin.FromIndunType(indunType));

    [Theory]
    [InlineData(24, TempleEventType.Chaos)]
    [InlineData(4, TempleEventType.BorderDefenseWar)]
    public void TheTempleEventSubOpensASignUpWithItsSeconds(short indunType, TempleEventType type)
    {
        var bifrost = new BifrostSession();
        var dispatcher = new PacketDispatcher();
        new BifrostReader(bifrost, () => 0).Register(dispatcher);
        (int Seconds, TempleEventType Type)? opened = null;
        bifrost.BifrostTimeEvent += (seconds, opening) => opened = (seconds, opening);

        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.TempleEvent);
        p.WriteShort(indunType);
        p.WriteShort(540);
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.Equal((540, type), opened);
    }

    [Fact]
    public void ATempleEventForAnUnknownTypeOpensNothing()
    {
        var bifrost = new BifrostSession();
        var dispatcher = new PacketDispatcher();
        new BifrostReader(bifrost, () => 0).Register(dispatcher);
        bool opened = false;
        bifrost.BifrostTimeEvent += (_, _) => opened = true;

        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.TempleEvent);
        p.WriteShort(47);
        p.WriteShort(540);
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.False(opened);
    }

    [Fact]
    public void TheJoinRequestCarriesTheWireType()
    {
        var sink = new RecordingSink();
        new BifrostRequests(sink).Join(TempleEventType.UnderTheCastle);

        var bytes = sink.Sent!.GetBytes();
        Assert.Equal((byte)GameOpcodes.GS_EVENT, bytes[0]);
        Assert.Equal((byte)TempleSubOpcode.TempleEventJoin, bytes[1]);
        Assert.Equal(TempleJoin.UnderTheCastleIndunType, System.BitConverter.ToUInt16(bytes, 2));
        Assert.Equal(4, bytes.Length);
    }

    private sealed class RecordingSink : IPacketSink
    {
        public Packet? Sent;

        public bool Connected => true;

        public void Send(Packet packet) => Sent = packet;
    }
}
