using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _changeHairLayer = null!;
    private HudWindow _changeHairPanel = null!;
    private LookPreview _changeHairPreview = null!;
    private LookEditor _changeHairEditor = null!;
    private Label _changeHairStatus = null!;
    private Label _changeHairName = null!;
    private Label _changeHairRace = null!;
    private Button _changeHairApply = null!;
    private bool _changeHairShown, _changeHairInFlight;

    private void ChangeHairInit()
    {
        BuildChangeHairPanel();
        Net.I.ChangeHairResultEvent += OnChangeHairResult;
        Net.I.BeautyShopEvent += OpenChangeHair;
    }

    private void ChangeHairDispose()
    {
        Net.I.ChangeHairResultEvent -= OnChangeHairResult;
        Net.I.BeautyShopEvent -= OpenChangeHair;
    }

    private void BuildChangeHairPanel()
    {
        _changeHairLayer = new CanvasLayer { Layer = 74 };
        AddChild(_changeHairLayer);
        _changeHairPanel = new HudWindow("changehair", "Beauty Shop") { Visible = false };
        _changeHairPanel.SetMeta("classic_appearance_controls", 1);
        _changeHairPanel.Closed += CloseChangeHair;
        _changeHairLayer.AddChild(_changeHairPanel);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12);
        _changeHairPanel.Body.AddChild(row);
        _changeHairPreview = new LookPreview(LookPreviewWidth, LookPreviewHeight) { Name = "look_preview" };
        row.AddChild(LookPreviewColumn(_changeHairPreview));
        var form = new VBoxContainer(); form.AddThemeConstantOverride("separation", 8); row.AddChild(form);
        _changeHairName = HudStyle.Label(14); _changeHairName.Name = "look_name"; form.AddChild(_changeHairName);
        _changeHairRace = HudStyle.Label(13); _changeHairRace.Name = "look_identity"; form.AddChild(_changeHairRace);
        _changeHairEditor = new LookEditor { Name = "look_editor" };
        _changeHairEditor.GetNode<Control>("look_race_heading").Hide();
        _changeHairEditor.GetNode<Control>("look_races").Hide();
        _changeHairEditor.Changed += ShowChangeHairLook;
        form.AddChild(_changeHairEditor);
        _changeHairStatus = HudStyle.Label(13);
        _changeHairStatus.Name = "look_status";
        _changeHairStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _changeHairStatus.CustomMinimumSize = new Vector2(230, 0); form.AddChild(_changeHairStatus);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 8); form.AddChild(actions);
        _changeHairApply = LookActionButton("Apply", SubmitChangeHair); _changeHairApply.Name = "look_accept"; actions.AddChild(_changeHairApply);
        var cancel = LookActionButton("Cancel", CloseChangeHair); cancel.Name = "look_cancel"; actions.AddChild(cancel);
    }

    private void OpenChangeHair()
    {
        if (_changeHairShown || _changeHairInFlight) return;
        var me = Net.I.LastEnter;
        _changeHairEditor.SetLocked(false);
        _changeHairEditor.Load(System.Array.Empty<int>(), me.Race, _selfFace, _selfHair);
        _changeHairName.Text = me.Name;
        _changeHairRace.Text = Domain.StarterStats.RaceName(me.Race);
        _changeHairApply.Disabled = false;
        ShowChangeHairLook(); SetChangeHairStatus("", false);
        _changeHairPanel.Show(); _changeHairShown = true;
    }

    private void ToggleChangeHair()
    {
        if (_changeHairShown) CloseChangeHair(); else OpenChangeHair();
    }

    private void CloseChangeHair()
    {
        if (!_changeHairShown) return;
        _changeHairShown = false; _changeHairPanel.Hide();
        _changeHairEditor.CloseColourPicker(); _changeHairPreview.Clear();
    }

    private void ShowChangeHairLook()
    {
        if (_changeHairInFlight) return;
        _changeHairEditor.SetColourAvailable(CharacterPreview.HairCount(_changeHairEditor.Race) > 0);
        _changeHairEditor.Refresh();
        _changeHairPreview.Show(_changeHairEditor.Race, _changeHairEditor.Face, _changeHairEditor.HairStyle, _changeHairEditor.HairColour);
    }

    private void SubmitChangeHair()
    {
        if (!_changeHairShown || _changeHairInFlight || _selfDead) return;
        _changeHairInFlight = true;
        _changeHairEditor.SetLocked(true); _changeHairApply.Disabled = true;
        SetChangeHairStatus("Applying…", false);
        if (!Net.I.SendChangeHair(_changeHairEditor.Hair, _changeHairEditor.Face))
            OnChangeHairResult(false, 0, 0);
    }

    private void OnChangeHairResult(bool ok, int face, int hair)
    {
        if (!_changeHairInFlight) return;
        _changeHairInFlight = false;
        _changeHairEditor.SetLocked(false); _changeHairApply.Disabled = false;
        if (ok)
        {
            _selfHair = hair; _selfFace = face;
            RerenderSelfEquipment();
            ChatStatusNotice("Your new look is ready."); CloseChangeHair();
        }
        else if (_changeHairShown) SetChangeHairStatus("The stylist couldn't apply that.", true);
        else ChatStatusNotice("The stylist couldn't apply that.");
    }

    private void SetChangeHairStatus(string text, bool warn)
    {
        _changeHairStatus.Text = text;
        _changeHairStatus.AddThemeColorOverride("font_color", warn ? UiTheme.Bad : Colors.White);
    }
}
