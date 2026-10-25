namespace LibreKO.Domain;

public static class NpcNames
{
    private static readonly Dictionary<int, string> npcNames = new();
    private static readonly Dictionary<int, string> mobNames = new();
    private static bool loaded;

    internal static Action? Loader { get; set; }

    internal static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        Loader?.Invoke();
    }

    internal static void AddNpc(int npcId, string name) => npcNames[npcId] = name;

    internal static void AddMonster(int npcId, string name) => mobNames[npcId] = name;

    public static string Name(int npcId, bool isMonster)
    {
        EnsureLoaded();
        var map = isMonster ? mobNames : npcNames;
        if (map.TryGetValue(npcId, out var n) && n.Length > 0)
            return n;
        return (isMonster ? "Mob #" : "NPC #") + npcId;
    }

    public static string? Known(int npcId)
    {
        EnsureLoaded();
        return npcNames.TryGetValue(npcId, out var n) && n.Length > 0 ? n : null;
    }

    public static List<(int Id, string Name)> Search(string query, bool monsters, int cap)
    {
        EnsureLoaded();
        var map = monsters ? mobNames : npcNames;
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = new List<(int Id, string Name)>();
        if (int.TryParse(query.Trim(), out int wanted) && map.TryGetValue(wanted, out var exact)) found.Add((wanted, exact));
        if (words.Length == 0) return found;
        found.AddRange(map
            .Where(entry => entry.Value.Length > 0 && entry.Key != wanted && words.All(word => entry.Value.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Value.StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(entry => entry.Value.Length)
            .ThenBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key)
            .Take(cap)
            .Select(entry => (entry.Key, entry.Value)));
        return found;
    }

    public static string TalkName(int npcId)
    {
        EnsureLoaded();
        if (npcNames.TryGetValue(npcId, out var n) && n.Length > 0) return n;
        if (mobNames.TryGetValue(npcId, out var m) && m.Length > 0) return m;
        return "NPC #" + npcId;
    }
}
