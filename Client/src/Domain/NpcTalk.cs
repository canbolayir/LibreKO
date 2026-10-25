using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public sealed record NpcShoutLine(int Chance, bool Chat, string Text);

public sealed record NpcShoutSet(double IntervalSeconds, IReadOnlyList<NpcShoutLine> Lines);

public sealed record MonsterTalkLine(string Text, int Chance);

public sealed class NpcTalkTable
{
    public Dictionary<int, NpcShoutSet> Shouts { get; } = new();
    public Dictionary<int, MonsterTalkLine?[]> Monsters { get; } = new();

    public NpcShoutSet? ShoutsOf(int proto) => Shouts.TryGetValue(proto, out var set) ? set : null;

    public MonsterTalkLine? TalkOf(int proto, int slot) =>
        slot >= 0 && Monsters.TryGetValue(proto, out var lines) && slot < lines.Length ? lines[slot] : null;
}

public static class NpcTalk
{
    public const string SelfNameTag = "<selfname>";
    public const int NoBracket = 100;
    public const int DeathSlot = 5;
    public const int RollRange = 100;
    private const int ChanceScale = 100;
    private const int MinShoutType = 10;
    private const int FirstSilentType = 82;
    private const int LastSilentType = 87;
    private const int FirstMoradonZone = 21;
    private const int LastMoradonZone = 25;
    private const int AllNations = 3;
    private const string TablePath = "res://assets/npcs/npc_talk.json";

    private const int DeathBracket = 0;
    private static readonly (int Floor, int Bracket)[] BracketFloors = { (80, 99), (60, 80), (40, 60), (20, 40), (1, 20) };

    private static NpcTalkTable? table;

    public static NpcTalkTable Table => table ??= GameFiles.ReadText(TablePath) is { } json ? Parse(json) : new NpcTalkTable();

    public static bool Shouts(int npcType, int zone, int npcNation, int myNation) =>
        npcType >= MinShoutType && npcType is not (>= FirstSilentType and <= LastSilentType)
        && (zone is >= FirstMoradonZone and <= LastMoradonZone || npcNation == AllNations || npcNation == myNation);

    public static NpcShoutLine? Pick(NpcShoutSet set, int roll)
    {
        int total = 0;
        foreach (var line in set.Lines)
        {
            total = Math.Min(RollRange, total + line.Chance / ChanceScale);
            if (total > roll) return line;
        }
        return null;
    }

    public static int Bracket(int hp, int maxHp)
    {
        int percent = maxHp > 0 ? (int)(hp / (float)maxHp * NoBracket) : NoBracket;
        if (percent >= NoBracket) return NoBracket;
        foreach (var (floor, bracket) in BracketFloors)
            if (percent >= floor) return bracket;
        return DeathBracket;
    }

    public static int Slot(int bracket)
    {
        if (bracket == DeathBracket) return DeathSlot;
        return Array.FindIndex(BracketFloors, b => b.Bracket == bracket);
    }

    public static string Fill(string text, string selfName) => text.Replace(SelfNameTag, selfName, StringComparison.Ordinal);

    public static NpcTalkTable Parse(string json)
    {
        var parsed = new NpcTalkTable();
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.GetProperty("shouts").EnumerateArray())
        {
            var lines = new List<NpcShoutLine>();
            foreach (var l in e.GetProperty("lines").EnumerateArray())
                lines.Add(new NpcShoutLine(l.GetProperty("chance").GetInt32(), l.GetProperty("chat").GetBoolean(), l.GetProperty("text").GetString() ?? ""));
            parsed.Shouts[e.GetProperty("proto").GetInt32()] = new NpcShoutSet(e.GetProperty("interval").GetInt32(), lines);
        }
        foreach (var e in doc.RootElement.GetProperty("monsters").EnumerateArray())
        {
            var lines = new List<MonsterTalkLine?>();
            foreach (var l in e.GetProperty("lines").EnumerateArray())
                lines.Add(l.ValueKind == JsonValueKind.Null ? null
                    : new MonsterTalkLine(l.GetProperty("text").GetString() ?? "", l.GetProperty("chance").GetInt32()));
            parsed.Monsters[e.GetProperty("proto").GetInt32()] = lines.ToArray();
        }
        return parsed;
    }
}
