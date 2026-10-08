using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _msgrLayer = null!;
    private HudWindow _msgrPanel = null!;
    private VBoxContainer _msgrList = null!;
    private LineEdit _msgrToInput = null!;
    private LineEdit _msgrTextInput = null!;
    private bool _msgrShown;

    private void MessengerInit()
    {
        _msgrLayer = new CanvasLayer { Layer = 74 };
        AddChild(_msgrLayer);
        _msgrPanel = new HudWindow("messenger", "Messenger") { Visible = false };
        _msgrPanel.SetMeta("classic_communication_controls", 1);
        _msgrPanel.Closed += CloseMessenger;
        _msgrLayer.AddChild(_msgrPanel);
        var root = _msgrPanel.Body;
        root.AddThemeConstantOverride("separation", 6);

        root.AddChild(UiTheme.SectionTitle("Online"));
        var scroll = new ScrollContainer { Name = "messenger_scroll", CustomMinimumSize = new Vector2(320, 240), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        _msgrList = new VBoxContainer { Name = "messenger_list", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _msgrList.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_msgrList);

        root.AddChild(UiTheme.SectionTitle("Quick Whisper"));
        _msgrToInput = new LineEdit { Name = "messenger_to", PlaceholderText = "To (name)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 20 };
        root.AddChild(_msgrToInput);
        var sendRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        sendRow.AddThemeConstantOverride("separation", 6);
        root.AddChild(sendRow);
        _msgrTextInput = new LineEdit { Name = "messenger_message", PlaceholderText = "Message", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 128 };
        _msgrTextInput.TextSubmitted += _ => DoMessengerSend();
        sendRow.AddChild(_msgrTextInput);
        var sendBtn = new Button { Name = "messenger_send", Text = "Send", FocusMode = Control.FocusModeEnum.None };
        sendBtn.Pressed += DoMessengerSend;
        sendRow.AddChild(sendBtn);

        Net.I.MessengerListEvent += OnMessengerList;
    }

    private void MessengerDispose()
    {
        Net.I.MessengerListEvent -= OnMessengerList;
    }

    private void ToggleMessenger()
    {
        if (_msgrShown) { CloseMessenger(); return; }
        _msgrPanel.Visible = true;
        _msgrShown = true;
        Net.I.SendMessengerList();
    }

    private void CloseMessenger()
    {
        if (!_msgrShown) return;
        _msgrShown = false;
        _msgrPanel.Visible = false;
    }

    private void DoMessengerSend()
    {
        string to = _msgrToInput.Text.Trim();
        string text = _msgrTextInput.Text.Trim();
        if (to.Length == 0 || text.Length == 0) return;
        Net.I.SendMessengerMessage(to, text);
        _msgrTextInput.Clear();
    }

    private void OnMessengerList(List<MessengerBuddy> list)
    {
        foreach (var c in _msgrList.GetChildren()) { _msgrList.RemoveChild(c); c.QueueFree(); }
        foreach (var b in list)
        {
            var row = new PanelContainer();
            row.SetMeta("communication_row", "buddy"); row.SetMeta("communication_online", b.Online);
            row.AddThemeStyleboxOverride("panel", UiTheme.Row());
            var hb = new HBoxContainer(); hb.AddThemeConstantOverride("separation", 8);
            row.AddChild(hb);
            var name = UiTheme.Text(b.Name, 13, b.Online ? UiTheme.TextHi : UiTheme.TextLo);
            name.Name = "communication_name"; name.TooltipText = b.Name;
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hb.AddChild(name);
            var state = UiTheme.Text(b.Online ? "Online" : "Offline", 12, b.Online ? UiTheme.Gold : UiTheme.TextLo);
            state.Name = "communication_state"; hb.AddChild(state);
            string targetName = b.Name;
            var whisper = new Button { Name = "communication_action", Text = "Whisper", FocusMode = Control.FocusModeEnum.None };
            whisper.Pressed += () => { _msgrToInput.Text = targetName; _msgrTextInput.GrabFocus(); };
            hb.AddChild(whisper);
            _msgrList.AddChild(row);
        }
        if (_msgrList.GetChildCount() == 0)
        {
            var e = HudStyle.Label(13); e.Text = "No one else online.";
            _msgrList.AddChild(e);
        }
    }
}
