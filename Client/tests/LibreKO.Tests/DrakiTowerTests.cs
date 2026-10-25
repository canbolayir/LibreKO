using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class DrakiTowerTests
{
    private const uint DefaultFinish = 3600;
    private const uint DefaultStage = 1;

    private static Packet RankList(byte myRank, uint entrancesLeft)
    {
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.DrakiList);
        p.WriteByte(1);
        p.WriteSByteString("Zeus");
        p.WriteUInt(895);
        p.WriteUInt(12);
        for (int i = 1; i < DrakiWire.TopRows; i++)
        {
            p.WriteByte((byte)(i + 1));
            p.WriteSByteString("");
            p.WriteUInt(DefaultFinish);
            p.WriteUInt(DefaultStage);
        }
        p.WriteByte(myRank);
        p.WriteSByteString("Rikka");
        p.WriteUInt(1200);
        p.WriteUInt(3);
        p.WriteUInt(7);
        p.WriteUInt(entrancesLeft);
        p.ResetOffset();
        return p;
    }

    private static (PacketDispatcher Dispatcher, GameSession Session) Readers()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new NoSink(), session, new LoginState()), () => 0);
        return (dispatcher, session);
    }

    private sealed class NoSink : IPacketSink
    {
        public bool Connected => false;

        public void Send(Packet packet) { }
    }

    [Fact]
    public void TheRankListReadsTheTopRowsMyRowAndTheEntrances()
    {
        var (dispatcher, session) = Readers();
        DrakiRankList? list = null;
        session.Bifrost.DrakiRankListEvent += received => list = received;

        dispatcher.Dispatch(RankList(myRank: 40, entrancesLeft: 2));

        Assert.NotNull(list);
        Assert.Equal(DrakiWire.TopRows, list!.Top.Count);
        Assert.Equal(new DrakiRankRow(1, "Zeus", 895, 12), list.Top[0]);
        Assert.Equal(new DrakiRankRow(40, "Rikka", 1200, 3), list.Mine);
        Assert.Equal(7, list.MaxStage);
        Assert.Equal(2, list.EntrancesLeft);
    }

    [Fact]
    public void AStageEndFreezesTheTimerWithItsOutcome()
    {
        var (dispatcher, session) = Readers();
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.DrakiLeaveSecond);
        p.WriteByte(0x0C);
        p.WriteByte(0x04);
        p.WriteUShort(4);
        p.WriteUShort(2);
        p.WriteUInt(310);
        p.WriteByte(1);
        p.ResetOffset();

        dispatcher.Dispatch(p);

        Assert.Equal(new DrakiStageEnd(4, 2, 310, GaveUp: true), session.Bifrost.DrakiFinish);
        Assert.Equal(new DrakiPlateView(4, 2, 0, 310, Finished: true, GaveUp: true), session.Bifrost.DrakiPlate(60_000));
    }

    [Fact]
    public void TheLimitCountsDownAndTheElapsedTimeUpFromTheTimerPacket()
    {
        var bifrost = new BifrostSession();
        bifrost.ApplyDrakiTimer(3, 2, 300, 100, 1_000);

        Assert.Equal(new DrakiPlateView(3, 2, 300, 100, false, false), bifrost.DrakiPlate(1_000));
        Assert.Equal(new DrakiPlateView(3, 2, 240, 160, false, false), bifrost.DrakiPlate(61_000));
        Assert.Equal(0, bifrost.DrakiSecondsLeft(400_000));
    }

    [Fact]
    public void TheElapsedTimeStandsStillWhileTheSubStageIsZero()
    {
        var bifrost = new BifrostSession();
        bifrost.ApplyDrakiTimer(2, 0, 180, 50, 0);

        Assert.Equal(50, bifrost.DrakiElapsedSeconds(30_000));
        Assert.Equal(150, bifrost.DrakiSecondsLeft(30_000));
    }

    [Fact]
    public void AStageEndFreezesTheLimitWhereItWas()
    {
        var bifrost = new BifrostSession();
        bifrost.ApplyDrakiTimer(3, 2, 300, 100, 0);
        bifrost.ApplyDrakiStageEnd(new DrakiStageEnd(3, 2, 400, GaveUp: false), 20_000);

        Assert.Equal(new DrakiPlateView(3, 2, 280, 400, true, false), bifrost.DrakiPlate(90_000));
    }

    [Fact]
    public void TheLeaveNoticeHidesThePlateTenSecondsLater()
    {
        var bifrost = new BifrostSession();
        int notices = 0;
        bifrost.DrakiClosingEvent += () => notices++;
        bifrost.ApplyDrakiTimer(1, 1, 300, 0, 0);
        Assert.True(bifrost.DrakiPlateShown(1_000_000));

        bifrost.ApplyDrakiLeave(5_000);

        Assert.Equal(1, notices);
        Assert.True(bifrost.DrakiPlateClosing);
        Assert.True(bifrost.DrakiPlateShown(14_999));
        Assert.False(bifrost.DrakiPlateShown(15_000));
    }

    [Fact]
    public void ANewTimerReopensAClosingPlate()
    {
        var bifrost = new BifrostSession();
        bifrost.ApplyDrakiTimer(1, 1, 300, 0, 0);
        bifrost.ApplyDrakiLeave(0);
        bifrost.ApplyDrakiTimer(1, 3, 300, 40, 2_000);

        Assert.False(bifrost.DrakiPlateClosing);
        Assert.True(bifrost.DrakiPlateShown(60_000));
    }

    [Fact]
    public void OnlyATimerWithASubStageRaisesTheFloorBanner()
    {
        var bifrost = new BifrostSession();
        var floors = new List<(int, int)>();
        int timers = 0;
        bifrost.DrakiFloorEvent += (stage, subStage) => floors.Add((stage, subStage));
        bifrost.DrakiTimerEvent += () => timers++;

        bifrost.ApplyDrakiTimer(2, 0, 180, 0, 0);
        bifrost.ApplyDrakiTimer(2, 3, 300, 0, 0);

        Assert.Equal(2, timers);
        Assert.Equal(new[] { (2, 3) }, floors);
    }

    [Fact]
    public void TheLargePlateLabelsItsLinesAndTheSmallOneShowsOnlyTheValues()
    {
        var running = new DrakiPlateView(3, 2, 299, 65, false, false);

        Assert.Equal(new DrakiPlateLines("Draki's Tower: 3-2", "Time Limit 04:59", "Elapsed Time 01:05"), DrakiTower.PlateLines(running, large: true));
        Assert.Equal(new DrakiPlateLines("3-2", "04:59", "01:05"), DrakiTower.PlateLines(running, large: false));
    }

    [Theory]
    [InlineData(false, "Clear")]
    [InlineData(true, "Give Up")]
    public void AFinishedLargePlateShowsTheOutcomeInPlaceOfTheLimit(bool gaveUp, string outcome)
    {
        var finished = new DrakiPlateView(5, 3, 120, 1800, true, gaveUp);

        Assert.Equal(outcome, DrakiTower.PlateLines(finished, large: true).Limit);
        Assert.Equal("02:00", DrakiTower.PlateLines(finished, large: false).Limit);
    }

    [Theory]
    [InlineData(1, 1, "1.The Preparation - 1", "Tower Entrance")]
    [InlineData(2, 6, "2.The Beginning - 6", "A Girl's Parents 2")]
    [InlineData(5, 3, "5.The Ultimate Stage - 3", "Draki's Madness")]
    [InlineData(3, 2, "3.The Result - 2", "")]
    public void TheFloorBannerNamesTheStageAndTheFloor(int stage, int subStage, string floor, string name) =>
        Assert.Equal(new DrakiFloorTexts(floor, name), DrakiTower.FloorTexts(stage, subStage));

    [Fact]
    public void TheEnterRequestCarriesTheCertificateAndTheChapter()
    {
        var bytes = DrakiWire.Enter(DrakiTower.CertificateItem, 3).GetBytes();

        Assert.Equal((byte)GameOpcodes.GS_EVENT, bytes[0]);
        Assert.Equal((byte)TempleSubOpcode.DrakiEnter, bytes[1]);
        Assert.Equal(DrakiTower.CertificateItem, System.BitConverter.ToInt32(bytes, 2));
        Assert.Equal(3, bytes[6]);
    }

    [Theory]
    [InlineData(895, "14:55")]
    [InlineData(3600 + 65, "01:05")]
    [InlineData(0, "00:00")]
    public void FinishTimesShowMinutesAndSecondsWithinTheHour(int seconds, string expected) =>
        Assert.Equal(expected, DrakiTower.FinishTime(seconds));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void OnlyTheFirstHundredAreRanked(int rank, bool expected) =>
        Assert.Equal(expected, DrakiTower.Ranked(rank));

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, false)]
    [InlineData(5, 9, true)]
    [InlineData(6, 9, false)]
    [InlineData(0, 5, false)]
    public void OnlyTheSectorsUpToTheHighestReachedAreOpen(int sector, int maxStage, bool expected) =>
        Assert.Equal(expected, DrakiTower.SectorOpen(sector, maxStage));

    [Fact]
    public void TheBoardStaysOpenWhenTheServerAsksForIt() =>
        Assert.Null(DrakiTower.EnterText(DrakiEnterResult.ShowBoard));

    [Theory]
    [InlineData(DrakiEnterResult.NoLimit, 40069)]
    [InlineData(DrakiEnterResult.TowerFull, 40076)]
    [InlineData(DrakiEnterResult.LevelTooLow, 40077)]
    [InlineData(DrakiEnterResult.CannotEnterNow, 40077)]
    [InlineData(DrakiEnterResult.SilentRefusal, 40090)]
    public void EnterRefusalsUseTheirTexts(DrakiEnterResult result, int text) =>
        Assert.Equal(text, DrakiTower.EnterText(result)?.Id);
}
