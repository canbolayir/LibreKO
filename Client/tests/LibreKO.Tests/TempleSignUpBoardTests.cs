using System;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class TempleSignUpBoardTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TwoSignUpsStayOpenSideBySide()
    {
        var board = new TempleSignUpBoard();

        Assert.True(board.OpenSignUp(TempleEventType.BorderDefenseWar, 600, Now));
        Assert.True(board.OpenSignUp(TempleEventType.UnderTheCastle, 600, Now));
        Assert.False(board.OpenSignUp(TempleEventType.UnderTheCastle, 590, Now));

        Assert.Equal(new[] { TempleEventType.BorderDefenseWar, TempleEventType.UnderTheCastle }, board.Open);
        Assert.Equal(TempleEventType.UnderTheCastle, board.Current);
    }

    [Fact]
    public void ANewSignUpDoesNotPullThePlayerAwayFromTheOneTheyJoined()
    {
        var board = new TempleSignUpBoard();
        board.OpenSignUp(TempleEventType.ForgottenTemple, 600, Now);
        board.SetRegistered(TempleEventType.ForgottenTemple);

        board.OpenSignUp(TempleEventType.Chaos, 600, Now);

        Assert.Equal(TempleEventType.ForgottenTemple, board.Current);
        Assert.True(board.Select(TempleEventType.Chaos));
        Assert.Equal(TempleEventType.Chaos, board.Current);
    }

    [Fact]
    public void ClosingTheShownSignUpFallsBackToTheOtherAndDropsItsRegistration()
    {
        var board = new TempleSignUpBoard();
        board.OpenSignUp(TempleEventType.BorderDefenseWar, 600, Now);
        board.OpenSignUp(TempleEventType.UnderTheCastle, 600, Now);
        board.SetRegistered(TempleEventType.UnderTheCastle);

        board.Close(TempleEventType.UnderTheCastle);

        Assert.Equal(TempleEventType.BorderDefenseWar, board.Current);
        Assert.Equal(TempleEventType.None, board.Registered);
    }

    [Fact]
    public void SignUpsExpireOnTheirOwnClock()
    {
        var board = new TempleSignUpBoard();
        board.OpenSignUp(TempleEventType.Chaos, 60, Now);
        board.OpenSignUp(TempleEventType.ForgottenTemple, 600, Now);

        Assert.Equal(new[] { TempleEventType.Chaos }, board.Expire(Now.AddSeconds(60)));
        Assert.Equal(540, board.SecondsLeft(TempleEventType.ForgottenTemple, Now.AddSeconds(60)));
        Assert.Equal(TempleEventType.ForgottenTemple, board.Current);
    }

    [Fact]
    public void EveryEventRoundTripsThroughItsWireType()
    {
        Assert.Equal(TempleJoin.ChaosIndunType, TempleJoin.IndunType(TempleEventType.Chaos));
        Assert.Equal(TempleJoin.BorderDefenseWarIndunType, TempleJoin.IndunType(TempleEventType.BorderDefenseWar));
        foreach (var type in new[] { TempleEventType.Chaos, TempleEventType.BorderDefenseWar, TempleEventType.JuraidMountain,
                     TempleEventType.UnderTheCastle, TempleEventType.ForgottenTemple })
            Assert.Equal(type, TempleJoin.FromIndunType(TempleJoin.IndunType(type)));
    }
}
