using Godot;
using LibreKO.Network;

namespace LibreKO.Plugins;

public readonly record struct GamePanelRow(string Id, string[] Cells, string Hint, Color Color, bool Online = true,
    bool Trackable = false, bool Abandonable = false, bool Claimable = false, bool Tracked = false);

/// <summary>Character-window presentation over the client's existing services.</summary>
public interface IGameCharacterPanel
{
    string SelectedPage { get; }
    string RaceName { get; }
    string JobName { get; }
    string LevelLabel { get; }
    string TitleName { get; }
    MyClanInfo Clan { get; }
    int QuestFilter { get; }
    int QuestKind { get; }
    IReadOnlyList<Window> Dialogs { get; }
    int StatBonus(int row);
    IReadOnlyList<GamePanelRow> Rows(string section);
    string Status(string section);
    void SelectPage(string page);
    void Refresh(string section);
    void Act(string action, string selection = "", string value = "");
}
