using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public sealed class PetBarLayout
{
    public const int Size = PetSkills.SlotsPerPage * PetSkills.Pages;

    private readonly int[] slots = new int[Size];
    private readonly bool[] auto = new bool[Size];

    public bool Saved { get; private set; }

    public int this[int slot] => slot is >= 0 and < Size ? slots[slot] : 0;

    public bool IsAuto(int slot) => slot is >= 0 and < Size && auto[slot] && slots[slot] != 0;

    public bool ToggleAuto(int slot)
    {
        if (slot is < 0 or >= Size || slots[slot] == 0) return false;
        auto[slot] = !auto[slot];
        Saved = true;
        return true;
    }

    public static PetBarLayout InIdOrder(IReadOnlyList<int> skills)
    {
        var layout = new PetBarLayout();
        for (int i = 0; i < Size && i < skills.Count; i++) layout.slots[i] = skills[i];
        return layout;
    }

    public bool Place(int slot, int skillId, int fromSlot)
    {
        if (slot is < 0 or >= Size || skillId <= 0 || slot == fromSlot) return false;
        if (fromSlot is >= 0 and < Size)
        {
            slots[fromSlot] = slots[slot];
            (auto[fromSlot], auto[slot]) = (auto[slot], auto[fromSlot]);
        }
        else auto[slot] = false;
        slots[slot] = skillId;
        Saved = true;
        return true;
    }

    public bool Clear(int slot)
    {
        if (slot is < 0 or >= Size || slots[slot] == 0) return false;
        slots[slot] = 0;
        auto[slot] = false;
        Saved = true;
        return true;
    }

    public void KeepOnly(Func<int, bool> belongs)
    {
        for (int i = 0; i < Size; i++)
            if (slots[i] != 0 && !belongs(slots[i])) (slots[i], auto[i]) = (0, false);
    }

    public static PetBarLayout? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("slots", out var list) || list.ValueKind != JsonValueKind.Array) return null;
            var layout = new PetBarLayout { Saved = true };
            int i = 0;
            foreach (var cell in list.EnumerateArray())
            {
                if (i >= Size) break;
                layout.slots[i++] = cell.TryGetInt32(out int id) && id > 0 ? id : 0;
            }
            if (doc.RootElement.TryGetProperty("auto", out var flagged) && flagged.ValueKind == JsonValueKind.Array)
                foreach (var cell in flagged.EnumerateArray())
                    if (cell.TryGetInt32(out int slot) && slot is >= 0 and < Size && layout.slots[slot] != 0) layout.auto[slot] = true;
            return layout;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Write()
    {
        var flagged = new List<int>();
        for (int i = 0; i < Size; i++)
            if (IsAuto(i)) flagged.Add(i);
        return JsonSerializer.Serialize(new { slots, auto = flagged });
    }
}
