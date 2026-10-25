using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public sealed record BossPhase(
    int Proto, string Name, int Look, int Phase, int MaxPhase, bool Buried, float GroundOffset, string? AnimSet,
    int RightHand, int LeftHand, int ScalePercent, int ShoutId, int Weather)
{
    public int PlugSlots => LeftHand != 0 ? (RightHand != 0 ? 2 : 1) : (RightHand != 0 ? 1 : 0);

    public int Layer => BossPhases.Layer(Phase, MaxPhase);
}

public sealed record BossShout(int Id, string Text, double IntervalSeconds, bool Banner);

public sealed class BossPhaseTable
{
    public Dictionary<int, BossPhase> ByProto { get; } = new();
    public Dictionary<int, BossShout> Shouts { get; } = new();

    public BossPhase? For(int proto) => ByProto.TryGetValue(proto, out var phase) ? phase : null;

    public BossShout? Shout(int id) => Shouts.TryGetValue(id, out var shout) ? shout : null;
}

public static class BossPhases
{
    public const int Zone = TempleZones.UnderTheCastle;
    public const int WeatherAmount = 100;
    public const int MaxLayer = 4;
    private const double MaxShoutInterval = 10.0;
    private const string TablePath = "res://assets/npcs/boss_phases.json";

    private static BossPhaseTable? table;

    public static BossPhaseTable Table => table ??= GameFiles.ReadText(TablePath) is { } json ? Parse(json) : new BossPhaseTable();

    public static BossPhase? For(int zone, bool npc, int proto) => zone == Zone && npc ? Table.For(proto) : null;

    public static int Layer(int phase, int maxPhase)
    {
        bool counted = maxPhase is > 0 and <= MaxLayer;
        int layer = counted ? MaxLayer - maxPhase + phase : phase;
        bool shown = counted ? layer < MaxLayer : layer <= MaxLayer;
        return shown && layer >= 0 ? layer : 0;
    }

    public static int[] Gear(BossPhase phase)
    {
        var gear = new int[InventoryConstants.VisualSlotCount];
        gear[InventoryConstants.VisRightHand] = phase.RightHand;
        gear[InventoryConstants.VisLeftHand] = phase.LeftHand;
        return gear;
    }

    public static BossPhaseTable Parse(string json)
    {
        var parsed = new BossPhaseTable();
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.GetProperty("phases").EnumerateArray())
        {
            var phase = new BossPhase(
                e.GetProperty("proto").GetInt32(), e.GetProperty("name").GetString() ?? "", e.GetProperty("look").GetInt32(),
                e.GetProperty("phase").GetInt32(), e.GetProperty("maxPhase").GetInt32(), e.GetProperty("buried").GetBoolean(),
                e.GetProperty("groundOffset").GetSingle(),
                e.GetProperty("anim").ValueKind == JsonValueKind.String ? e.GetProperty("anim").GetString() : null,
                e.GetProperty("right").GetInt32(), e.GetProperty("left").GetInt32(), (int)e.GetProperty("scale").GetSingle(),
                e.GetProperty("shout").GetInt32(), e.GetProperty("weather").GetInt32());
            parsed.ByProto.TryAdd(phase.Proto, phase);
        }
        foreach (var e in doc.RootElement.GetProperty("shouts").EnumerateArray())
        {
            var shout = new BossShout(e.GetProperty("id").GetInt32(), e.GetProperty("text").GetString() ?? "",
                Math.Min(MaxShoutInterval, e.GetProperty("interval").GetInt32()), e.GetProperty("banner").GetBoolean());
            parsed.Shouts[shout.Id] = shout;
        }
        return parsed;
    }
}

public sealed class BossShoutQueue
{
    private readonly List<(int NpcId, BossShout Shout)> waiting = new();
    private readonly HashSet<(int NpcId, int ShoutId)> queued = new();
    private double quietUntil = double.MinValue;

    public bool Enqueue(int npcId, BossShout shout)
    {
        if (!queued.Add((npcId, shout.Id))) return false;
        waiting.Add((npcId, shout));
        return true;
    }

    public (int NpcId, BossShout Shout)? Next(double now, bool held, Func<int, bool> present)
    {
        if (held || now < quietUntil) return null;
        while (waiting.Count > 0)
        {
            var next = waiting[0];
            waiting.RemoveAt(0);
            if (!present(next.NpcId)) continue;
            quietUntil = now + next.Shout.IntervalSeconds;
            return next;
        }
        return null;
    }

    public void Clear()
    {
        waiting.Clear();
        queued.Clear();
        quietUntil = double.MinValue;
    }
}
