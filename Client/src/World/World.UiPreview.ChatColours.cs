using Godot;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildChatColoursClassicUiPreview()
    {
        Chat = new ChatSystem(this); Chat.Build(); Chat.DetachNetwork();
        BuildChatColors(); OpenChatColors();
        Chat.Panel.Reparent(_chatColorsLayer!, false); Chat.Panel.Visible = false;
        RemoveChild(_chatColorsLayer!); return _chatColorsLayer!;
    }
}
