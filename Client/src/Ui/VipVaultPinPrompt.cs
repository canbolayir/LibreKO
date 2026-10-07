using System;
using Godot;

namespace LibreKO;

public sealed partial class VipVaultPinPrompt : CanvasLayer
{
    public HudWindow Window { get; }
    public Label Message { get; }
    public LineEdit Input { get; }
    public Button ConfirmButton { get; }
    public event Action? Confirmed;

    public VipVaultPinPrompt()
    {
        Layer = 76; Visible = false;
        var blocker = new ColorRect { Color = new Color(0, 0, 0, .35f), MouseFilter = Control.MouseFilterEnum.Stop };
        blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(blocker);
        Window = new HudWindow("vipwarehouse_pin", "VIP Vault PIN"); AddChild(Window);
        Window.Closed += Hide;
        Message = UiTheme.Text("Enter your 4-digit PIN:", 13, UiTheme.TextHi); Window.Body.AddChild(Message);
        Input = new LineEdit { Name = "vault_pin", MaxLength = 4, Secret = true, CustomMinimumSize = new Vector2(160, 0) };
        Window.Body.AddChild(Input);
        var buttons = new HBoxContainer(); Window.Body.AddChild(buttons);
        var cancel = new Button { Name = "vault_pin_cancel", Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        ConfirmButton = new Button { Name = "vault_pin_confirm", Text = "Confirm", FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += Hide; ConfirmButton.Pressed += () => Confirmed?.Invoke();
        buttons.AddChild(ConfirmButton); buttons.AddChild(cancel);
    }

    public void PopupCentered()
    {
        Window.Visible = Visible = true;
        Window.Position = ((Window.GetViewportRect().Size - Window.Size) / 2).Round();
    }
    public override void _UnhandledInput(InputEvent ev)
    {
        if (!Visible || ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        Hide(); GetViewport().SetInputAsHandled();
    }
}
