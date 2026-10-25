using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class RankMarkTests : GameTestBase
{
    [Theory]
    [InlineData(1, RankMarkTier.Gold)]
    [InlineData(2, RankMarkTier.Silver)]
    [InlineData(3, RankMarkTier.Silver)]
    [InlineData(4, RankMarkTier.Bronze)]
    [InlineData(10, RankMarkTier.Bronze)]
    [InlineData(11, null)]
    [InlineData(0, null)]
    public void PlacesOneToTenEarnAMark(int place, RankMarkTier? tier) =>
        RankMarks.TierFor(place, 1, 3, 10).Should().Be(tier);

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(3, false)]
    [InlineData(8, false)]
    public void TheClanBoardAndTheMissingBoardThreeEarnNone(byte board, bool earns) =>
        RankMarkService.EarnsMarks(board).Should().Be(earns);

    [Fact]
    public void MarksSurviveASave()
    {
        var marks = new RankMarks();
        marks.Award(0, RankMarkTier.Gold);
        marks.Award(0, RankMarkTier.Gold);
        marks.Award(6, RankMarkTier.Bronze);
        marks.AwardRibbon(RankMarks.KingRibbon);

        var loaded = RankMarks.Load(marks.Serialize());

        loaded.Boards.Should().Equal(((byte)0, (ushort)0, (ushort)0, (ushort)2), ((byte)6, (ushort)1, (ushort)0, (ushort)0));
        loaded.Ribbons.Should().Equal((RankMarks.KingRibbon, (ushort)1));
        RankMarks.Load([]).Boards.Should().BeEmpty();
        RankMarks.Load([1, 0]).Boards.Should().BeEmpty("a cut record is dropped, not thrown");
    }

    [Fact]
    public void TheOwnListIsThe2619TwoBlocks()
    {
        var marks = new RankMarks();
        marks.Award(1, RankMarkTier.Silver);
        marks.AwardRibbon(RankMarks.KingRibbon);

        var packet = RankMarkPacketWriter.Own(marks);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_RANK_MARKS);
        packet.ReadByte().Should().Be(1);
        packet.ReadUShort().Should().Be(1);
        packet.ReadShort().Should().Be(0);
        packet.ReadShort().Should().Be(1);
        packet.ReadShort().Should().Be(0);
        packet.ReadByte().Should().Be(1);
        packet.ReadUShort().Should().Be(RankMarks.KingRibbon);
        packet.ReadUShort().Should().Be(1);
    }

    [Fact]
    public async Task TheMonthEndPaysOnlineAndOfflineHunters()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var service = provider.GetRequiredService<IRankMarkService>();
        var characters = provider.CreateScope().ServiceProvider.GetRequiredService<ICharacterRepository>();
        var offline = new Character { Name = "Rikka", AccountId = 9 };
        await characters.CreateAsync(offline);
        var sent = new List<Packet>();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var online = sessions.CreateSession(client, characterId: 777, accountId: 778);
        online.Name = "Zeus";

        await service.AwardMonthAsync([
            new DailyRankPlace { Board = 0, SubjectId = 777, Place = 1 },
            new DailyRankPlace { Board = 1, SubjectId = offline.Id, Place = 5 },
            new DailyRankPlace { Board = 8, SubjectId = 777, Place = 1 },
        ]);
        await service.AwardKingAsync("Zeus");

        online.RankMarks.Boards.Should().Equal(((byte)0, (ushort)0, (ushort)0, (ushort)1));
        online.RankMarks.Ribbons.Should().Equal((RankMarks.KingRibbon, (ushort)1));
        sent.Should().Contain(p => p.GetOpcode() == (byte)GameOpcodes.GS_RANK_MARKS);
        var stored = await provider.CreateScope().ServiceProvider.GetRequiredService<ICharacterRepository>().GetById(offline.Id);
        RankMarks.Load(stored!.RankMarks).Boards.Should().Equal(((byte)1, (ushort)1, (ushort)0, (ushort)0));
    }
}
