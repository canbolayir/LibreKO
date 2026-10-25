using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class SheriffBoardTests
{
    private const string King = "Zeus";

    [Theory]
    [InlineData(King, false, true)]
    [InlineData("Rikka", true, true)]
    [InlineData("Rikka", false, false)]
    public void TheKingAndGameMastersFileAndVote(string name, bool gameMaster, bool allowed)
    {
        var gameData = Substitute.For<IGameDataService>();
        gameData.KingSystemTable.Returns(new Dictionary<byte, KingSystemData>
        {
            [(byte)AccountNation.Karus] = new() { Nation = (byte)AccountNation.Karus, KingName = King },
        });
        var session = new UserSession(Substitute.For<IClient>(), 1, 1)
        {
            Name = name,
            Nation = AccountNation.Karus,
            Fame = 100,
            IsGM = gameMaster,
        };

        MiscPacketCoordinator.CanUseSheriff(gameData, session).Should().Be(allowed);
    }
}
