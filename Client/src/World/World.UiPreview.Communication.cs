using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildCommunicationUiPreview(string id)
    {
        CanvasLayer layer;
        if (id == "messenger")
        {
            MessengerInit(); layer = _msgrLayer; _msgrShown = true; _msgrPanel.Visible = true;
            OnMessengerList(new List<MessengerBuddy>
            {
                new() { CharId = 1, Name = "Rikka", Online = true },
                new() { CharId = 2, Name = "LongCharacterName1299", Online = false },
                new() { CharId = 3, Name = "Ariel", Online = true },
            });
        }
        else
        {
            ChatRoomInit(); layer = _chatRoomLayer; _chatRoomShown = true; _chatRoomPanel.Visible = true;
            _chatRoomCurrentId = 7; UpdateChatRoomStatus();
            OnChatRoomList(new List<ChatRoomEntry>
            {
                new() { RoomId = 7, Name = "Moradon hunting party", MemberCount = 8 },
                new() { RoomId = 8, Name = "A long room name for the market", MemberCount = 123 },
                new() { RoomId = 9, Name = "Ronark raid", MemberCount = 24 },
            });
            AppendChatRoomLine("System", "Joined the room.", UiTheme.Gold);
            AppendChatRoomLine("Rikka", "Meet near the bridge after stocking potions.", UiTheme.TextHi);
            AppendChatRoomLine("LongCharacterName1299", "This is a long message that should wrap inside the fixed chat viewport. We are looking for a warrior and a priest for the next hunt.", UiTheme.TextHi);
        }
        RemoveChild(layer); return layer;
    }
}
