using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class TeleportFriendTests
{
    private static readonly FriendEntry[] Friends =
    {
        new() { Name = "Rikka", CharId = 7, Status = 1 },
        new() { Name = "Zeus", CharId = -1, Status = 0 },
    };

    [Theory]
    [InlineData(500038, true)]
    [InlineData(510038, true)]
    [InlineData(500106, false)]
    [InlineData(500306, false)]
    public void OnlyTheFriendListScrollsPickFromTheFriendList(int skillId, bool picks) =>
        Assert.Equal(picks, TeleportFriend.PicksFromFriendList(skillId));

    [Fact]
    public void TheSelectedOnlineFriendIsTheTarget() => Assert.Equal(7, TeleportFriend.Target("Rikka", Friends));

    [Theory]
    [InlineData(null)]
    [InlineData("Zeus")]
    [InlineData("Nobody")]
    public void NoSelectedOnlineFriendMeansNoTarget(string? selected) =>
        Assert.Equal(TeleportFriend.NoFriend, TeleportFriend.Target(selected, Friends));
}
