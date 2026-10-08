using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class LookEditor : VBoxContainer
{
    private readonly VBoxContainer _races = new() { Name = "look_races" };
    private readonly List<(int Race, Button Button)> _raceButtons = new();
    private readonly List<(Button Button, bool Face)> _steps = new();
    private readonly Label _faceLbl, _hairLbl;
    private readonly ColorPickerButton _colour;
    private bool _locked;
    private bool _colourAvailable = true;

    public int Race { get; private set; }
    public int Face { get; private set; }
    public int HairStyle { get; private set; }
    public Color HairColour => _colour.Color;
    public int Hair => HairCode.Pack(HairStyle, _colour.Color);

    public event Action? Changed;

    public LookEditor()
    {
        AddThemeConstantOverride("separation", 8);
        CustomMinimumSize = new Vector2(230, 0);
        var raceTitle = UiTheme.SectionTitle("Race"); raceTitle.Name = "look_race_heading"; AddChild(raceTitle);
        _races.AddThemeConstantOverride("separation", 4);
        AddChild(_races);
        var appearanceTitle = UiTheme.SectionTitle("Appearance"); appearanceTitle.Name = "look_appearance_heading"; AddChild(appearanceTitle);
        _faceLbl = StepperRow("Face", dir => { Face = Wrap(Face + dir, CharacterPreview.FaceCount(Race)); Changed?.Invoke(); });
        _hairLbl = StepperRow("Hair", dir => { HairStyle = Wrap(HairStyle + dir, CharacterPreview.HairCount(Race)); Changed?.Invoke(); });

        var colourRow = new HBoxContainer { Name = "look_colour_row" };
        colourRow.AddThemeConstantOverride("separation", 8);
        var colourLbl = HudStyle.Label(13);
        colourLbl.Text = "Hair colour";
        colourLbl.CustomMinimumSize = new Vector2(80, 0);
        colourRow.AddChild(colourLbl);
        _colour = new ColorPickerButton
        {
            Name = "look_colour",
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            EditAlpha = false,
        };
        _colour.ColorChanged += _ => { if (!_locked && _colourAvailable) Changed?.Invoke(); };
        _colour.GetPopup().AddChild(new LookColourPopupInput(_colour.GetPopup()));
        colourRow.AddChild(_colour);
        AddChild(colourRow);
    }

    public void Load(IReadOnlyList<int> races, int race, int face, int hair)
    {
        foreach (var (_, button) in _raceButtons)
        {
            _races.RemoveChild(button);
            button.QueueFree();
        }
        _raceButtons.Clear();
        foreach (int option in races)
        {
            int picked = option;
            var button = UiTheme.TopTabButton(StarterStats.RaceName(option), 13);
            button.Name = "look_race_" + option;
            button.Pressed += () => PickRace(picked);
            _races.AddChild(button);
            _raceButtons.Add((option, button));
        }
        Race = Contains(races, race) || races.Count == 0 ? race : races[0];
        Face = face;
        HairStyle = HairCode.StyleOf(hair);
        _colour.Color = HairCode.ColourOf(hair);
        Clamp();
        Refresh();
    }

    public void Refresh()
    {
        foreach (var (race, button) in _raceButtons)
        {
            button.SetPressedNoSignal(race == Race);
            button.Disabled = _locked;
        }
        int faces = CharacterPreview.FaceCount(Race), hairs = CharacterPreview.HairCount(Race);
        _faceLbl.Text = faces == 0 ? "-" : (Face + 1).ToString();
        _hairLbl.Text = hairs == 0 ? "-" : (HairStyle + 1).ToString();
        foreach (var step in _steps) step.Button.Disabled = _locked || (step.Face ? faces : hairs) <= 1;
        _colour.Disabled = _locked || !_colourAvailable;
        _colour.Modulate = _colourAvailable ? Colors.White : new Color(.45f, .45f, .45f, 1);
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (locked) CloseColourPicker();
        Refresh();
    }

    public void CloseColourPicker() => _colour.GetPopup().Hide();

    public void SetColourAvailable(bool available)
    {
        _colourAvailable = available;
        if (!available) CloseColourPicker();
        Refresh();
    }

    private void PickRace(int race)
    {
        if (_locked) return;
        Race = race;
        Clamp();
        Changed?.Invoke();
    }

    private void Clamp()
    {
        int faces = CharacterPreview.FaceCount(Race), hairs = CharacterPreview.HairCount(Race);
        // Missing local variants must not silently overwrite a saved appearance.
        if (faces > 0) Face = Mathf.Clamp(Face, 0, faces - 1);
        if (hairs > 0) HairStyle = Mathf.Clamp(HairStyle, 0, hairs - 1);
    }

    private static bool Contains(IReadOnlyList<int> races, int race)
    {
        foreach (int option in races)
            if (option == race) return true;
        return false;
    }

    private static int Wrap(int value, int count)
    {
        int n = Mathf.Max(1, count);
        return ((value % n) + n) % n;
    }

    private Label StepperRow(string label, Action<int> step)
    {
        string id = "look_" + label.ToLowerInvariant();
        var row = new HBoxContainer { Name = id + "_row" };
        row.AddThemeConstantOverride("separation", 6);
        var name = HudStyle.Label(13);
        name.Text = label;
        name.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(name);
        var previous = StepButton("<", () => { if (!_locked) step(-1); }); previous.Name = id + "_previous";
        row.AddChild(previous);
        var value = HudStyle.Label(13, HorizontalAlignment.Center);
        value.CustomMinimumSize = new Vector2(36, 0);
        value.Name = id + "_value";
        row.AddChild(value);
        var next = StepButton(">", () => { if (!_locked) step(1); }); next.Name = id + "_next";
        row.AddChild(next);
        _steps.Add((previous, label == "Face")); _steps.Add((next, label == "Face"));
        AddChild(row);
        return value;
    }

    public static Button StepButton(string text, Action pressed)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(30, 24), FocusMode = FocusModeEnum.None };
        btn.Pressed += pressed;
        return btn;
    }
}

public partial class LookColourPopupInput : Node
{
    private readonly PopupPanel _popup;
    public LookColourPopupInput(PopupPanel popup) => _popup = popup;
    public override void _Input(InputEvent ev)
    {
        if (!_popup.Visible || ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        _popup.Hide(); GetViewport().SetInputAsHandled();
    }
}
