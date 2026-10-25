using LibreKO.Domain;

namespace LibreKO.Plugins;

public readonly record struct GameTempleSignUp(TempleEventType Type, string Title, int SecondsLeft, bool Registered,
    string? Hint, int Karus, int ElMorad);

public interface IGameTempleSignUps
{
    IReadOnlyList<GameTempleSignUp> Open { get; }

    void Join(TempleEventType type);
    void Cancel();

    event Action? Changed;
}

public sealed partial class PluginGame
{
    public IGameTempleSignUps TempleSignUps => templeSignUps;

    private readonly TempleSignUpsProxy templeSignUps = new();

    internal void AttachTempleSignUps(IGameTempleSignUps templeSignUpsSource)
    {
        templeSignUps.Source = templeSignUpsSource;
        RaiseTempleSignUps();
    }

    internal void RaiseTempleSignUps() => templeSignUps.Raise();

    private sealed class TempleSignUpsProxy : IGameTempleSignUps
    {
        public IGameTempleSignUps? Source;
        public event Action? Changed;
        internal void Raise() => Changed?.Invoke();

        public IReadOnlyList<GameTempleSignUp> Open => Source?.Open ?? Array.Empty<GameTempleSignUp>();
        public void Join(TempleEventType type) => Source?.Join(type);
        public void Cancel() => Source?.Cancel();
    }
}
