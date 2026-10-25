using System.Text.Json;

namespace LibreKO.Domain;

public sealed class QuickslotSet
{
    public string Name { get; set; } = "";
    public int[] Cells { get; } = new int[QuickslotSaves.CellsPerSet];

    public bool IsEmpty => Array.TrueForAll(Cells, id => id == 0);

    public QuickslotSet Copy()
    {
        var copy = new QuickslotSet { Name = Name };
        Array.Copy(Cells, copy.Cells, Cells.Length);
        return copy;
    }

    public bool Place(int cell, int skillId, int fromCell)
    {
        if (cell is < 0 or >= QuickslotSaves.CellsPerSet || skillId <= 0 || cell == fromCell) return false;
        if (fromCell is >= 0 and < QuickslotSaves.CellsPerSet) Cells[fromCell] = Cells[cell];
        Cells[cell] = skillId;
        return true;
    }

    public bool Clear(int cell)
    {
        if (cell is < 0 or >= QuickslotSaves.CellsPerSet || Cells[cell] == 0) return false;
        Cells[cell] = 0;
        return true;
    }

    public void Reset() => Array.Clear(Cells);
}

public sealed class QuickslotSaves
{
    public const int Sets = 32;
    public const int RowsPerPage = 4;
    public const int Pages = Sets / RowsPerPage;
    public const int CellsPerSet = 10;
    public const int MaxNameLength = 32;
    public const int FirstItemSkill = 490000;
    public const int EmptyNameText = 44500;
    public const string EmptyNameFallback = "Empty Slot %d";
    public const int ApplyQuestionText = 44501;
    public const string ApplyQuestionFallback = "Will you apply %s in Quickslot %d?";
    public const int TitleText = 44502;
    public const string TitleFallback = "Skill Slot Save";
    public const int EmptySetText = 44503;
    public const string EmptySetFallback = "This slot is an empty slot.";
    public const int NameAskText = 44504;
    public const string NameAskFallback = "Please select a name to save.";
    public const int SelectText = 44505;
    public const string SelectFallback = "Please select a slot.";
    public const int UnusableText = 44506;
    public const string UnusableFallback = "Unusuable skill(s) are included.";

    private readonly QuickslotSet[] sets = new QuickslotSet[Sets];

    private QuickslotSaves(string emptyName)
    {
        for (int i = 0; i < Sets; i++) sets[i] = new QuickslotSet { Name = EmptyName(emptyName, i) };
    }

    public QuickslotSet this[int index] => sets[index];

    public static int Index(int page, int row) => page * RowsPerPage + row;

    public static string EmptyName(string template, int index) => template.Replace("%d", (index + 1).ToString());

    public static string ApplyQuestion(string template, string name, int hotbarPage)
    {
        int at = template.IndexOf("%s", StringComparison.Ordinal);
        string named = at < 0 ? template : template[..at] + name + template[(at + 2)..];
        return named.Replace("%d", hotbarPage.ToString());
    }

    public static bool Usable(int skillId, Func<int, bool> learned) =>
        skillId == 0 || skillId >= FirstItemSkill || learned(skillId);

    public static QuickslotSaves Fresh(string emptyName) => new(emptyName);

    public void Store(int index, QuickslotSet set)
    {
        if (index is < 0 or >= Sets) return;
        var stored = set.Copy();
        if (stored.Name.Length > MaxNameLength) stored.Name = stored.Name[..MaxNameLength];
        sets[index] = stored;
    }

    public static QuickslotSaves Parse(string? json, string emptyName)
    {
        var saves = new QuickslotSaves(emptyName);
        if (string.IsNullOrWhiteSpace(json)) return saves;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("sets", out var list) || list.ValueKind != JsonValueKind.Array) return saves;
            int i = 0;
            foreach (var entry in list.EnumerateArray())
            {
                if (i >= Sets) break;
                var set = saves.sets[i++];
                if (entry.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    set.Name = name.GetString() ?? set.Name;
                if (!entry.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Array) continue;
                int c = 0;
                foreach (var cell in cells.EnumerateArray())
                {
                    if (c >= CellsPerSet) break;
                    set.Cells[c++] = cell.TryGetInt32(out int id) && id > 0 ? id : 0;
                }
            }
        }
        catch (JsonException)
        {
        }
        return saves;
    }

    public string Write() => JsonSerializer.Serialize(new
    {
        sets = sets.Select(set => new { name = set.Name, cells = set.Cells }),
    });
}
