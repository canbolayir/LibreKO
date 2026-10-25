using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public sealed class QuestPrefs
{
    public HashSet<int> Tracked { get; } = new();
    public HashSet<int> Offered { get; } = new();

    public static QuestPrefs Parse(string? json)
    {
        var prefs = new QuestPrefs();
        if (string.IsNullOrWhiteSpace(json)) return prefs;
        try
        {
            using var doc = JsonDocument.Parse(json);
            Read(doc.RootElement, "tracked", prefs.Tracked);
            Read(doc.RootElement, "offered", prefs.Offered);
        }
        catch (JsonException)
        {
            return new QuestPrefs();
        }
        return prefs;
    }

    public string Write() => JsonSerializer.Serialize(new { tracked = Sorted(Tracked), offered = Sorted(Offered) });

    private static void Read(JsonElement root, string name, HashSet<int> into)
    {
        if (!root.TryGetProperty(name, out var list) || list.ValueKind != JsonValueKind.Array) return;
        foreach (var id in list.EnumerateArray())
            if (id.TryGetInt32(out int questId)) into.Add(questId);
    }

    private static List<int> Sorted(HashSet<int> ids)
    {
        var list = new List<int>(ids);
        list.Sort();
        return list;
    }
}
