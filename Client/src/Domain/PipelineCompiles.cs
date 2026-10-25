using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public static class PipelineCompiles
{
    public static readonly string[] Kinds = { "canvas", "mesh", "surface", "draw", "specialization" };

    public static string Describe(IReadOnlyList<long> added)
    {
        long total = added.Sum();
        if (total == 0) return "pipelines +0";
        var parts = Kinds.Select((kind, i) => i < added.Count && added[i] > 0 ? $"{kind} {added[i]}" : null).Where(part => part != null);
        return $"pipelines +{total} ({string.Join(", ", parts)})";
    }
}
