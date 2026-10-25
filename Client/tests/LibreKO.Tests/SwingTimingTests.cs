using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SwingTimingTests
{
    [Theory]
    [InlineData(0, 0, 1f, 1f)]
    [InlineData(100, 100, 1f, 1f)]
    [InlineData(150, 100, 1f, 1.5f)]
    [InlineData(200, 90, 1f, 1.8f)]
    [InlineData(150, 0, 1f, 1.5f)]
    [InlineData(150, 100, 1.5f, 1f)]
    [InlineData(0, 0, 2f, 0.5f)]
    public void TheWeaponDelayAndItsExtPercentSetTheSwingInterval(int delay, int percent, float speed, float expected)
    {
        Assert.Equal(expected, SwingTiming.IntervalSeconds(delay, percent, speed), 3);
    }

    [Fact]
    public void OnlyAnAttackSpeedBuffSetsTheSwingSpeed()
    {
        Assert.True(SwingTiming.SetsAttackSpeed(SwingTiming.AttackSpeedBuffType, 120));
        Assert.False(SwingTiming.SetsAttackSpeed(18, 120));
        Assert.False(SwingTiming.SetsAttackSpeed(SwingTiming.AttackSpeedBuffType, 0));
        Assert.Equal(1.2f, SwingTiming.BuffFactor(120), 3);
    }

    [Theory]
    [InlineData(0, 0, 1f)]
    [InlineData(61, 0, 1f / 1.2f)]
    [InlineData(0, 62, 1f / 1.2f)]
    [InlineData(63, 0, 1f / 1.2f)]
    [InlineData(60, 11, 1f)]
    public void APickaxeBambooOrFishingRodInHandSlowsTheSwing(int rightKind, int leftKind, float expected) =>
        Assert.Equal(expected, SwingTiming.AnimationRate(1f, rightKind, leftKind), 3);
}
