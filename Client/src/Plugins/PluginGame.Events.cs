namespace LibreKO.Plugins;

public readonly record struct GameEventLine(string Name, bool Open, string When);

public interface IGameEvents
{
    IReadOnlyList<GameEventLine> Today { get; }

    void OpenWindow();

    event Action? Changed;
}

public sealed partial class PluginGame
{
    public IGameEvents Events => events;

    private readonly EventsProxy events = new();

    internal void AttachEvents(IGameEvents eventsSource)
    {
        events.Source = eventsSource;
        RaiseEvents();
    }

    internal void RaiseEvents() => events.Raise();

    private sealed class EventsProxy : IGameEvents
    {
        public IGameEvents? Source;
        public event Action? Changed;
        internal void Raise() => Changed?.Invoke();

        public IReadOnlyList<GameEventLine> Today => Source?.Today ?? Array.Empty<GameEventLine>();
        public void OpenWindow() => Source?.OpenWindow();
    }
}
