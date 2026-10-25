using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class FlashBonusTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, FlashKind.Drop)]
    [InlineData(2, FlashKind.Exp)]
    [InlineData(3, FlashKind.Contribution)]
    [InlineData(0, FlashKind.None)]
    [InlineData(4, FlashKind.None)]
    public void TheItemsSpecialAmountNamesTheFlash(int specialAmount, FlashKind kind) =>
        FlashBonusRules.KindOf(specialAmount).Should().Be(kind);

    [Fact]
    public void EachExpFlashAddsTenPercentUpToOneHundred()
    {
        var flash = new FlashBonusState();
        for (int i = 0; i < 12; i++) flash.Add(FlashKind.Exp, Now);

        flash.Bonus(FlashKind.Exp, Now).Should().Be(100);
        flash.CanAdd(FlashKind.Exp, Now).Should().BeFalse();
        flash.Bonus(FlashKind.Drop, Now).Should().Be(0);
    }

    [Fact]
    public void EachWarFlashAddsOneContributionUpToTen()
    {
        var flash = new FlashBonusState();
        for (int i = 0; i < 11; i++) flash.Add(FlashKind.Contribution, Now);

        flash.Bonus(FlashKind.Contribution, Now).Should().Be(10);
    }

    [Fact]
    public void AnotherKindReplacesTheFlashAndRestartsItsTime()
    {
        var flash = new FlashBonusState();
        flash.Add(FlashKind.Exp, Now);
        flash.Add(FlashKind.Exp, Now);

        var replaced = flash.Add(FlashKind.Drop, Now.AddMinutes(10));

        replaced.Should().Be(FlashKind.Exp);
        flash.Bonus(FlashKind.Exp, Now.AddMinutes(10)).Should().Be(0);
        flash.Bonus(FlashKind.Drop, Now.AddMinutes(10)).Should().Be(10);
        flash.EndsAtUtc.Should().Be(Now.AddMinutes(10) + FlashBonusRules.Duration);
    }

    [Fact]
    public void TheTimeStartsAtTheFirstUseAndAFlashEndsWhenItRunsOut()
    {
        var flash = new FlashBonusState();
        flash.Add(FlashKind.Exp, Now);
        flash.Add(FlashKind.Exp, Now.AddMinutes(20));

        flash.EndsAtUtc.Should().Be(Now + FlashBonusRules.Duration);
        flash.TakeExpired(Now.AddMinutes(29)).Should().Be(FlashKind.None);
        flash.Bonus(FlashKind.Exp, Now + FlashBonusRules.Duration).Should().Be(0);
        flash.TakeExpired(Now + FlashBonusRules.Duration).Should().Be(FlashKind.Exp);
        flash.Kind.Should().Be(FlashKind.None);
    }

    [Fact]
    public void TheStartPacketCarriesTheKindAndTheAmount()
    {
        var packet = FlashBonusPacketWriter.Started(FlashKind.Exp, 30);
        packet.ResetOffset();

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_STORY);
        packet.ReadByte().Should().Be((byte)PreGameSubOpcode.FlashBonus);
        packet.ReadByte().Should().Be(FlashBonusPacketWriter.ActionStarted);
        packet.ReadByte().Should().Be((byte)FlashKind.Exp);
        packet.ReadByte();
        packet.ReadInt().Should().Be(30);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public async Task UsingAnotherKindEndsTheOldTitleBeforeStartingTheNew()
    {
        var sessionManager = new SessionManager();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var session = sessionManager.CreateSession(client, characterId: 1, accountId: 1);
        var service = new FlashBonusService(sessionManager, NullLogger<FlashBonusService>.Instance, startTimer: false);

        await service.AddAsync(session, FlashKind.Exp);
        await service.AddAsync(session, FlashKind.Contribution);

        var sent = client.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IClient.SendPacket))
            .Select(call => (Packet)call.GetArguments()[0]!)
            .ToList();
        sent.Should().HaveCount(3);
        sent[1].ResetOffset();
        sent[1].ReadByte();
        sent[1].ReadByte().Should().Be(FlashBonusPacketWriter.ActionEnded);
        sent[1].ReadByte().Should().Be((byte)FlashKind.Exp);
        session.Flash.Bonus(FlashKind.Contribution, DateTime.UtcNow).Should().Be(1);
    }
}
