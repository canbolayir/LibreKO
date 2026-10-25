using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LibreKO.Tests;

public class KeyBindTests
{
    [Fact]
    public void EveryActionIsListedExactlyOnce()
    {
        var listed = KeyBinds.All.Select(e => e.Action).ToList();
        Assert.Equal(listed.Count, listed.Distinct().Count());
        Assert.Equal(Enum.GetValues<KeyAction>().Length, listed.Count);
    }

    [Fact]
    public void DefaultsAreAssignedAndUnique()
    {
        var seen = new Dictionary<(int, KeyChord), KeyAction>();
        foreach (var entry in KeyBinds.All)
        {
            var optional = entry.Action is KeyAction.PotionHp or KeyAction.PotionMp
                or KeyAction.CameraTurn or KeyAction.GameMenu or KeyAction.HotPageNext or KeyAction.NamePlates
                or KeyAction.PartyDrops or KeyAction.Party or KeyAction.Friends or KeyAction.Helmet or KeyAction.MyShop
                or KeyAction.Achievements;
            Assert.Equal(!optional, entry.Default.Assigned);
            if (optional) continue;
            var key = (KeyBinds.LayerOf(entry.Action), entry.Default);
            Assert.False(seen.TryGetValue(key, out var owner),
                $"{entry.Action} and {owner} both default to the same key");
            seen[key] = entry.Action;
        }
    }

    [Theory]
    [InlineData(KeyAction.Pet, Godot.Key.P)]
    [InlineData(KeyAction.TargetHostileAlt, Godot.Key.Q)]
    [InlineData(KeyAction.ToggleRun, Godot.Key.Y)]
    [InlineData(KeyAction.WhisperTarget, Godot.Key.F11)]
    [InlineData(KeyAction.HotPageBack, Godot.Key.Pageup)]
    [InlineData(KeyAction.HotPageForward, Godot.Key.Pagedown)]
    public void TheKeysMeanWhatTheyMeanInTwentySixNineteen(KeyAction action, Godot.Key key) =>
        Assert.Equal(key, KeyBinds.All.Single(e => e.Action == action).Default.Key);

    [Fact]
    public void NoWindowOfOursSitsOnTheTwentySixNineteenRenderToggle() =>
        Assert.DoesNotContain(KeyBinds.All, e => e.Default.Key == Godot.Key.O && !e.Default.Ctrl && !e.Default.Shift && !e.Default.Alt);

    [Fact]
    public void HotbarActionsAreContiguous()
    {
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(KeyAction.HotSlot1 + i, Enum.Parse<KeyAction>($"HotSlot{i + 1}"));
            Assert.Equal(KeyAction.HotPage1 + i, Enum.Parse<KeyAction>($"HotPage{i + 1}"));
        }
    }

    [Fact]
    public void ADefaultThatCollidesWithASavedChordStaysUnbound()
    {
        var table = KeyBinds.All;
        var first = table.First(e => e.Default.Assigned);
        var second = table.First(e => e.Default.Assigned && e.Action != first.Action && e.Default != first.Default);

        var bound = KeyBinds.Resolve(table, action => action == first.Action ? second.Default : null);

        Assert.Equal(second.Default, bound[first.Action]);
        Assert.Equal(KeyChord.Unbound, bound[second.Action]);
    }

    [Fact]
    public void CommanderKeysShareDigitsWithTheHotbarWithoutUnbindingEitherSide()
    {
        var bound = KeyBinds.Resolve(KeyBinds.All, action => action == KeyAction.HotSlot1 ? KeyBinds.All.First(e => e.Action == KeyAction.CommanderSlot1).Default : null);

        Assert.Equal(bound[KeyAction.HotSlot1], bound[KeyAction.CommanderSlot1]);
        Assert.True(bound[KeyAction.CommanderHold].Assigned);
        Assert.True(bound[KeyAction.ToggleRunAlt].Assigned);
    }

    [Fact]
    public void WithNothingSavedEveryActionTakesItsDefault()
    {
        var bound = KeyBinds.Resolve(KeyBinds.All, _ => null);

        Assert.All(KeyBinds.All, e => Assert.Equal(e.Default, bound[e.Action]));
    }
}
