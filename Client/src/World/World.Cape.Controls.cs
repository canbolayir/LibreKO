using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private static bool CapeSoldHere(int id, Cape.CapeDef def) => id is not 97 and not 98 and not 99 && (def.Price > 0 || def.Points > 0);
    private static Button CapeCell(string name) => new()
    {
        Name = name, ToggleMode = true, FocusMode = Control.FocusModeEnum.None,
        CustomMinimumSize = new Vector2(CapeSwatchSize, CapeSwatchSize), ClipContents = true,
    };

    private void BuildCapePages(VBoxContainer parent, bool pattern)
    {
        string prefix = pattern ? "cape_pattern_" : "cape_colour_";
        var row = new HBoxContainer(); parent.AddChild(row);
        var previous = new Button { Name = prefix + "previous", Text = "◀", FocusMode = Control.FocusModeEnum.None };
        var label = new Label { Name = prefix + "page", Text = "1", HorizontalAlignment = HorizontalAlignment.Center };
        var next = new Button { Name = prefix + "next", Text = "▶", FocusMode = Control.FocusModeEnum.None };
        row.AddChild(previous); row.AddChild(label); row.AddChild(next);
        previous.Pressed += () => ChangeCapePage(pattern, -1);
        next.Pressed += () => ChangeCapePage(pattern, 1);
        if (pattern) { _capePatternPrevious = previous; _capePatternNext = next; _capePatternPageLabel = label; }
        else { _capeColourPrevious = previous; _capeColourNext = next; _capeColourPageLabel = label; }
    }

    private void ChangeCapePage(bool pattern, int direction)
    {
        if (!CapeEditing) return;
        int count = pattern ? _capePatternIds.Count : _capeColourIds.Count;
        int capacity = pattern ? 4 : 6;
        int page = pattern ? _capePatternPage : _capeColourPage;
        page = Math.Clamp(page + direction, 0, Math.Max(0, (count - 1) / capacity));
        if (pattern) _capePatternPage = page; else _capeColourPage = page;
        RefreshCapePages();
    }

    private void RefreshCapePages()
    {
        if (_capePatternPageLabel == null || _capeColourPageLabel == null) return;
        for (int slot = 0; slot < 4; slot++)
        {
            int index = _capePatternPage * 4 + slot;
            var button = _capePatterns[slot];
            bool exists = index < _capePatternIds.Count;
            button.Disabled = !CapeEditing || !exists;
            button.SetPressedNoSignal(exists && _capePatternIds[index] == _capePattern);
            if (!exists) { ClearCapeArt(button); button.TooltipText = ""; continue; }
            int pattern = _capePatternIds[index];
            button.TooltipText = pattern == 0 ? "Plain" : $"Pattern {pattern}";
            FillCapeArt(button, CapePatternSampleColour, pattern, false);
            button.SetMeta("cape_catalogue_id", pattern);
        }
        for (int slot = 0; slot < 6; slot++)
        {
            int index = _capeColourPage * 6 + slot;
            var button = _capeColours[slot];
            bool exists = index < _capeColourIds.Count && Cape.TryGet(_capeColourIds[index], out _);
            button.Disabled = !CapeEditing || !exists;
            button.SetPressedNoSignal(exists && _capeColourIds[index] == _capeChoice);
            if (!exists) { ClearCapeArt(button); button.TooltipText = ""; continue; }
            int id = _capeColourIds[index]; Cape.TryGet(id, out var def);
            bool locked = MyClan.InClan && !CapeAllowed(def);
            FillCapeArt(button, def.C, def.M, locked);
            button.TooltipText = CapeCellTip(def, locked);
            button.SetMeta("cape_catalogue_id", id);
        }
        _capePatternPageLabel.Text = $"{_capePatternPage + 1}/{Math.Max(1, (_capePatternIds.Count + 3) / 4)}";
        _capeColourPageLabel.Text = $"{_capeColourPage + 1}/{Math.Max(1, (_capeColourIds.Count + 5) / 6)}";
        _capePatternPrevious.Disabled = !CapeEditing || _capePatternPage == 0;
        _capePatternNext.Disabled = !CapeEditing || (_capePatternPage + 1) * 4 >= _capePatternIds.Count;
        _capeColourPrevious.Disabled = !CapeEditing || _capeColourPage == 0;
        _capeColourNext.Disabled = !CapeEditing || (_capeColourPage + 1) * 6 >= _capeColourIds.Count;
    }

    private static void ClearCapeArt(Button button)
    {
        foreach (var child in button.GetChildren()) { button.RemoveChild(child); child.QueueFree(); }
    }

    private static void FillCapeArt(Button button, int colour, int pattern, bool locked)
    {
        if (button.GetMeta("cape_art_key", "").AsString() == $"{colour}:{pattern}:{locked}" && button.GetChildCount() > 0) return;
        ClearCapeArt(button);
        button.SetMeta("cape_art_key", $"{colour}:{pattern}:{locked}");
        var art = CapeSwatch(colour, pattern, Colors.White, locked);
        art.CustomMinimumSize = Vector2.Zero;
        art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(art);
    }

    private bool CapeCanApply()
    {
        if (!CapeImChief || MyClan.Flag < ClanTypes.Promoted || _selfDead) return false;
        bool dye = _capeR.Value != 0 || _capeG.Value != 0 || _capeB.Value != 0;
        if (_capeChoice < 0 && (!dye || !Cape.IsRenderable(_capeCurrent))) return false;
        if (_capeChoice >= 0)
        {
            if (!Cape.TryGet(_capeChoice, out var def)) return false;
            if (_capeTicket.ButtonPressed) return def.Price == 0 && MyClan.Grade <= 3;
            if (!CapeAllowed(def)) return false;
        }
        else if (_capeTicket.ButtonPressed) return false;
        return !dye || ClanTypes.AcceptsDonations(MyClan.Flag);
    }

    private string CapeCostText()
    {
        if (_capeTicket.ButtonPressed) return "1 castellan ticket";
        Cape.TryGet(_capeChoice, out var def);
        int points = def.Points + (_capeR.Value != 0 || _capeG.Value != 0 || _capeB.Value != 0 ? 36_000 : 0);
        var costs = new List<string>();
        if (def.Price > 0) costs.Add($"{def.Price:n0} Noahs");
        if (points > 0) costs.Add($"{points:n0} clan points");
        return costs.Count == 0 ? "Free" : string.Join("\n", costs);
    }

    private void CancelCapeConfirmation(int revision)
    {
        if (revision != _capeRevision || _capeNotice == null || _capeRequestInFlight) return;
        _capeNotice = null; UpdateCapeGate();
    }

    private void DismissCapeConfirmation()
    {
        _capeRevision++;
        if (_capeNotice is { } notice && GodotObject.IsInstanceValid(notice)) notice.Close();
        _capeNotice = null;
    }

    private void ResetCapePicker()
    {
        _capeRequestInFlight = false;
        CloseCape(); DismissCapeConfirmation();
        _capeChoice = NoCape;
    }
}
