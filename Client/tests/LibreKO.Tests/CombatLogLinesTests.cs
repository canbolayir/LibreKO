using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CombatLogLinesTests
{
    [Theory]
    [InlineData(21, 50, 50)]
    [InlineData(45, 50, 500)]
    [InlineData(57, 7, 70)]
    [InlineData(60, 7, 70)]
    [InlineData(85, 3, 30)]
    [InlineData(89, 3, 30)]
    [InlineData(86, 3, 3)]
    public void EventZonesLogHpTenTimesOver(int zone, int amount, int logged)
    {
        Assert.Equal(logged, CombatLogLines.LoggedHp(zone, amount));
    }

    [Theory]
    [InlineData(21, 1)]
    [InlineData(45, 10)]
    [InlineData(58, 10)]
    [InlineData(85, 10)]
    [InlineData(89, 10)]
    [InlineData(61, 1)]
    public void TheHpBarTextIsScaledInTheSameZones(int zone, int scale) =>
        Assert.Equal(scale, CombatLogLines.HpTextScale(zone));

    [Theory]
    [InlineData(101, true, CombatLogLines.SpRecoveredText)]
    [InlineData(208, false, CombatLogLines.SpUsedText)]
    [InlineData(113, false, CombatLogLines.SpUsedText)]
    [InlineData(103, true, CombatLogLines.MpRecoveredText)]
    [InlineData(212, false, CombatLogLines.MpUsedText)]
    public void WarriorsRoguesAndKuriansSeeSp(int classCode, bool recovered, int text)
    {
        Assert.Equal(text, CombatLogLines.ManaText(classCode, recovered));
    }
}
