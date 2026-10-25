using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public enum WearState : byte { Fine = 0, Worn = 1, Broken = 2 }

public readonly record struct FigureRect(int X, int Y, int Width, int Height);

public sealed record DurabilityPart(IReadOnlyList<FigureRect> Worn, FigureRect Broken, string Tooltip);

public sealed record DurabilityLayout(int Width, int Height, FigureRect Base, float Fps, IReadOnlyList<DurabilityPart> Parts);

public static class DurabilityFigure
{
    public const int Parts = 8;
    public const int ShieldKind = 60;
    private const double BrokenShare = 0.05;
    private const double WornShare = 0.3;

    public static WearState StateOf(int current, int max)
    {
        if (max <= 0) return WearState.Fine;
        if (current < max * BrokenShare) return WearState.Broken;
        return current < max * WornShare ? WearState.Worn : WearState.Fine;
    }

    public static int SlotOf(int part, int leftHandKind) => part switch
    {
        0 => leftHandKind == ShieldKind ? -1 : InventoryConstants.LeftHand,
        1 => InventoryConstants.RightHand,
        2 => leftHandKind == ShieldKind ? InventoryConstants.LeftHand : -1,
        3 => InventoryConstants.Foot,
        4 => InventoryConstants.Leg,
        5 => InventoryConstants.Breast,
        6 => InventoryConstants.Glove,
        7 => InventoryConstants.Head,
        _ => -1,
    };

    public static DurabilityLayout? ParseLayout(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var size = root.GetProperty("size");
            var parts = new List<DurabilityPart>();
            foreach (var part in root.GetProperty("parts").EnumerateArray())
            {
                var worn = new List<FigureRect>();
                foreach (var frame in part.GetProperty("worn").EnumerateArray()) worn.Add(Rect(frame));
                parts.Add(new DurabilityPart(worn, Rect(part.GetProperty("broken")),
                    part.TryGetProperty("tooltip", out var tip) ? tip.GetString() ?? "" : ""));
            }
            return new DurabilityLayout(size[0].GetInt32(), size[1].GetInt32(), Rect(root.GetProperty("base")),
                root.GetProperty("fps").GetSingle(), parts);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static FigureRect Rect(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32(), e[2].GetInt32(), e[3].GetInt32());
}
