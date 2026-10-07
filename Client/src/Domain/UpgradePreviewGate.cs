using System;

namespace LibreKO.Domain;

// Serialize previews on the existing protocol and reject replies for an edited selection.
public sealed class UpgradePreviewGate
{
    private int[]? _items, _positions;
    public bool Pending => _items != null;
    public bool Begin(int[] items, int[] positions)
    {
        if (Pending) return false;
        _items = (int[])items.Clone();
        _positions = (int[])positions.Clone();
        return true;
    }
    public bool Complete(int[] items, int[] positions)
    {
        bool current = _items != null && _items.AsSpan().SequenceEqual(items) && _positions!.AsSpan().SequenceEqual(positions);
        _items = _positions = null;
        return current;
    }
}
