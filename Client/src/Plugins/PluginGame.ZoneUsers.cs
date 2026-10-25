using Godot;

namespace LibreKO.Plugins;

public enum GameZoneUserState { Online, Merchant, Gathering }

public readonly record struct GameZoneUser(string Name, string Shown, bool Self, bool Enemy, Texture2D? ClanMark,
    GameZoneUserState State, float Distance);

public interface IGameZoneUsers
{
    IReadOnlyList<GameZoneUser> Users { get; }

    void Watch(bool shown);
    void ShowMenu(string name, Vector2 at);

    event Action? Changed;
}

public sealed partial class PluginGame
{
    public IGameZoneUsers ZoneUsers => zoneUsers;

    private readonly ZoneUsersProxy zoneUsers = new();

    internal void AttachZoneUsers(IGameZoneUsers zoneUsersSource)
    {
        zoneUsers.Source = zoneUsersSource;
        RaiseZoneUsers();
    }

    internal void RaiseZoneUsers() => zoneUsers.Raise();

    private sealed class ZoneUsersProxy : IGameZoneUsers
    {
        public IGameZoneUsers? Source;
        public event Action? Changed;
        internal void Raise() => Changed?.Invoke();

        public IReadOnlyList<GameZoneUser> Users => Source?.Users ?? Array.Empty<GameZoneUser>();
        public void Watch(bool shown) => Source?.Watch(shown);
        public void ShowMenu(string name, Vector2 at) => Source?.ShowMenu(name, at);
    }
}
