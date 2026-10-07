using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class AnvilPlacementRulesTests
{
    private static int[] Selection(params int[] materials)=>new[]{156210008}.Concat(materials).ToArray();
    [Fact] public void BlessedScrollAndTrinaUseTheForkSetting()
    {
        Assert.True(AnvilPlacementRules.Allows(Selection(379021000,700002000),false));
        Assert.True(AnvilPlacementRules.Allows(Selection(379021000,700002000),false,3,5,8,false));
    }
    [Fact] public void UnknownOriginAndMissingRecipeAreRejected()
    {
        Assert.False(AnvilPlacementRules.Allows(new[]{123456789,379021000},false));
        Assert.False(AnvilPlacementRules.Allows(Selection(379035000),false));
    }
    [Fact] public void ScrollAloneCanBePlacedBeforeProtection()
        =>Assert.True(AnvilPlacementRules.Allows(Selection(379021000),false,3,5,8,false));
    [Theory]
    [InlineData(156210008)] // Duplicate weapon.
    [InlineData(379159000)] // Accessory compound scroll.
    [InlineData(354000000)] // Accessory protection.
    [InlineData(810322000)] // No handler in this fork's upgrade service.
    [InlineData(379021001)] // Not an exact scroll identifier.
    [InlineData(379021999)]
    public void InvalidMaterialsAreRejected(int material)
        =>Assert.False(AnvilPlacementRules.Allows(Selection(material),false,3,5,8,false));
    [Fact] public void HighClassOriginRejectsLowClassScroll()
        =>Assert.False(AnvilPlacementRules.Allows(Selection(379221000),false,3,5,8,false));
    [Fact] public void TwoScrollsAreRejected()
        =>Assert.False(AnvilPlacementRules.Allows(Selection(379021000,379016000),false,3,5,8,false));
    [Fact] public void TwoProtectionsAreRejected()
        =>Assert.False(AnvilPlacementRules.Allows(Selection(379021000,700002000,890092000),false,3,5,8,false));
    [Fact] public void RebirthProtectionCannotMixWithNormalScroll()
        =>Assert.False(AnvilPlacementRules.Allows(Selection(379021000,379258000),false,3,5,8,false));
    [Fact] public void LogosHonorsTypeAndGradeLimit()
    {
        Assert.True(AnvilPlacementRules.Allows(Selection(379021000,890092000),false,3,5,9,false));
        Assert.False(AnvilPlacementRules.Allows(Selection(379021000,890092000),false,3,5,10,false));
        Assert.False(AnvilPlacementRules.Allows(Selection(379021000,890092000),false,3,1,9,false));
        Assert.False(AnvilPlacementRules.Allows(Selection(379256000,890092000),false,3,5,8,false));
    }
    [Fact] public void AccessoryMaterialsStayOnTheAccessoryBench()
    {
        Assert.True(AnvilPlacementRules.Allows(new[]{330620430,330620430,330620430,379159000,354000000},true,7,5,0,true));
        Assert.False(AnvilPlacementRules.Allows(new[]{330620430,330620430,330620430,379021000,700002000},true,7,5,0,true));
    }
}
