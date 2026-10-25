namespace LibreKO.Domain;

public static class TeleportFriend
{
    public const int NoFriendText = 7616;
    public const int NoFriend = -1;

    private static readonly int[] FriendListSkills = { 500038, 510038 };

    public static bool PicksFromFriendList(int skillId) => System.Array.IndexOf(FriendListSkills, skillId) >= 0;

    public static int Target(string? selectedName, IEnumerable<FriendEntry> friends)
    {
        if (string.IsNullOrEmpty(selectedName)) return NoFriend;
        foreach (var friend in friends)
            if (friend.Name == selectedName) return friend.IsOnline ? friend.CharId : NoFriend;
        return NoFriend;
    }
}
