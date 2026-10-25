using System.Collections.Generic;

namespace LibreKO.Domain;

public readonly record struct WhisperSlot(string Name, bool WantsOpen, float OpenHeight, float FoldedHeight);

public readonly record struct WhisperPlace(string Name, bool Open, bool Hidden, float Y);

public static class WhisperStack
{
    public static List<WhisperPlace> Plan(IReadOnlyList<WhisperSlot> order, float bottom, float top, float gap)
    {
        var places = new List<WhisperPlace>(order.Count);
        float y = bottom;
        foreach (var slot in order)
        {
            if (slot.WantsOpen && y - slot.OpenHeight >= top)
            {
                y -= slot.OpenHeight;
                places.Add(new WhisperPlace(slot.Name, true, false, y));
                y -= gap;
            }
            else if (y - slot.FoldedHeight >= top)
            {
                y -= slot.FoldedHeight;
                places.Add(new WhisperPlace(slot.Name, false, false, y));
                y -= gap;
            }
            else places.Add(new WhisperPlace(slot.Name, false, true, y));
        }
        return places;
    }
}
