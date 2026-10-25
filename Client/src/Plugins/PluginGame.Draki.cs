using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO.Plugins;

public interface IGameDraki
{
    bool PlateShown { get; }
    DrakiPlateView Plate { get; }
    DrakiRankList? Ranks { get; }

    void AskTown();
    void EnterChapter(int chapter);

    event Action? Changed;
    event Action<int, int>? FloorReached;
}

public sealed partial class PluginGame
{
    public IGameDraki Draki => draki;

    private readonly DrakiProxy draki = new();

    internal void AttachDraki(IGameDraki drakiSource)
    {
        draki.Source = drakiSource;
        RaiseDraki();
    }

    internal void RaiseDraki() => draki.Raise();

    internal void RaiseDrakiFloor(int stage, int subStage) => draki.RaiseFloor(stage, subStage);

    private sealed class DrakiProxy : IGameDraki
    {
        public IGameDraki? Source;
        public event Action? Changed;
        public event Action<int, int>? FloorReached;
        internal void Raise() => Changed?.Invoke();
        internal void RaiseFloor(int stage, int subStage) => FloorReached?.Invoke(stage, subStage);

        public bool PlateShown => Source?.PlateShown ?? false;
        public DrakiPlateView Plate => Source?.Plate ?? default;
        public DrakiRankList? Ranks => Source?.Ranks;
        public void AskTown() => Source?.AskTown();
        public void EnterChapter(int chapter) => Source?.EnterChapter(chapter);
    }
}
