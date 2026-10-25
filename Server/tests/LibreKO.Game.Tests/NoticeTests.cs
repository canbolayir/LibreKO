using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class NoticeTests
{
    [Fact]
    public void AScreenNoticeIsACountedListOfShortLines()
    {
        var packet = NoticePacketWriter.Screen("Server restarting", "In five minutes");

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_NOTICE);
        packet.ReadByte().Should().Be(NoticePacketWriter.ScreenNotice);
        packet.ReadByte().Should().Be(2);
        packet.ReadSByteString().Should().Be("Server restarting");
        packet.ReadSByteString().Should().Be("In five minutes");
    }

    [Fact]
    public void ALoginNoticeIsACountedListOfTitledEntries()
    {
        var packet = NoticePacketWriter.Login([("Exp Event", "Experience is doubled")]);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_NOTICE);
        packet.ReadByte().Should().Be(NoticePacketWriter.LoginNotice);
        packet.ReadByte().Should().Be(1);
        packet.ReadString().Should().Be("Exp Event");
        packet.ReadString().Should().Be("Experience is doubled");
    }

    [Fact]
    public void AnEventTitleIsStyleFourWithItsTitleAndMessage()
    {
        var packet = NoticePacketWriter.EventTitle("Clan Notice", "Siege at nine");

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_NOTICE);
        packet.ReadByte().Should().Be(NoticePacketWriter.EventTitles);
        packet.ReadByte().Should().Be(NoticePacketWriter.TitleShown);
        packet.ReadString().Should().Be("Clan Notice");
        packet.ReadString().Should().Be("Siege at nine");
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void RemovingAnEventTitleNamesOnlyTheTitle()
    {
        var packet = NoticePacketWriter.EventTitleRemoved("Exp Event");

        packet.ReadByte().Should().Be(NoticePacketWriter.EventTitles);
        packet.ReadByte().Should().Be(NoticePacketWriter.TitleRemoved);
        packet.ReadString().Should().Be("Exp Event");
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void OnlyRunningRateEventsAreListedWithTheirServerText()
    {
        var data = Substitute.For<IGameDataService>();
        data.GetServerResource(RateEvents.ResourceId(RateEvent.Exp)).Returns("Earn %d percent extra EXP.");
        var rates = new TimeWeatherBroadcastService(new SessionManager(), Substitute.For<ILogger<TimeWeatherBroadcastService>>())
        {
            ExpEventAmount = 50,
        };

        RateEvents.Active(rates, data).Should().Equal(("Exp Event", "Earn 50 percent extra EXP."));
    }

    [Fact]
    public async Task ATimedRateEventEndsByItselfAndOthersStay()
    {
        var rates = new TimeWeatherBroadcastService(new SessionManager(), Substitute.For<ILogger<TimeWeatherBroadcastService>>())
        {
            ExpEventAmount = 50,
            DropEventAmount = 20,
        };
        var start = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        rates.EndRateAt(RateEvent.Exp, start.AddMinutes(30));

        await rates.EndDueRatesAsync(start.AddMinutes(29));
        rates.ExpEventAmount.Should().Be(50);
        await rates.EndDueRatesAsync(start.AddMinutes(30));

        rates.ExpEventAmount.Should().Be(0);
        rates.RateEnd(RateEvent.Exp).Should().BeNull();
        rates.DropEventAmount.Should().Be(20);
    }

    [Fact]
    public void ALineTooLongForItsLengthByteIsCut()
    {
        var packet = NoticePacketWriter.Screen(new string('x', 400));

        packet.ReadByte();
        packet.ReadByte();
        packet.ReadSByteString().Length.Should().Be(255);
    }
}
