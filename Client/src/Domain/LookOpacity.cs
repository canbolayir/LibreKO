using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public static class LookOpacity
{
    public const float Opaque = 1f;
    private const string TablePath = "res://assets/npcs/look_opacity.json";

    private static Dictionary<int, float>? table;

    private static Dictionary<int, float> Table => table ??= GameFiles.ReadText(TablePath) is { } json ? Parse(json) : new();

    public static float Of(int lookId) => Table.TryGetValue(lookId, out float opacity) ? opacity : Opaque;

    public static Dictionary<int, float> Parse(string json)
    {
        var looks = new Dictionary<int, float>();
        using var doc = JsonDocument.Parse(json);
        foreach (var look in doc.RootElement.EnumerateObject())
            if (int.TryParse(look.Name, out int id) && look.Value.TryGetSingle(out float opacity))
                looks[id] = System.Math.Clamp(opacity, 0f, Opaque);
        return looks;
    }

    public static float Shown(float alpha, float lookOpacity) => System.Math.Clamp(alpha * lookOpacity, 0f, Opaque);
}
