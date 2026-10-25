using Godot;

namespace LibreKO.Plugins;

public readonly record struct GameManesBoard(bool Open, int SecondsLeft, int Participants, bool Applied);

public readonly record struct GameManesBar(bool Shown, string BossName, long BossHp, long BossMaxHp, int SecondsLeft, int NonBattleLeft, int Score);

public readonly record struct GameManesChoice(int Code, string Name, string Description, Texture2D? Icon, bool Held);

public readonly record struct GameManesPotion(int Code, string Name, string Price, Texture2D? Icon, bool Waiting);

public sealed record GameManesOffer(bool Bonus, string Title, IReadOnlyList<GameManesChoice> Skills, IReadOnlyList<GameManesPotion> Potions,
    int Selected, int Queued, bool Minimised, bool Blinking);

public readonly record struct GameManesPassive(int Code, string Name, Texture2D? Icon, int Count);

public readonly record struct GameManesTotals(string Hp, string AttackUp, string AttackDown);

public readonly record struct GameManesRankRow(string Rank, int Nation, string Kills, string Clan, string Name, string Score, bool Own);

public sealed record GameManesRanking(bool Shown, IReadOnlyList<GameManesRankRow> Rows, GameManesRankRow Mine);

public interface IGameManesSurvival
{
    GameManesBoard Board { get; }
    GameManesBar Bar { get; }
    GameManesOffer? Offer { get; }
    IReadOnlyList<GameManesPassive> Passives { get; }
    GameManesTotals Totals { get; }
    GameManesChoice? PassiveWon { get; }
    GameManesRanking Ranking { get; }

    void SignUp(bool apply);
    void Select(int index);
    void Complete();
    void Buy(int code);
    void Minimise();
    void Reopen();
    void ClosePassiveWon();

    event Action? Changed;
}

public sealed partial class PluginGame
{
    public IGameManesSurvival ManesSurvival => manesSurvival;

    private readonly ManesSurvivalProxy manesSurvival = new();

    internal void AttachManesSurvival(IGameManesSurvival manesSurvivalSource)
    {
        manesSurvival.Source = manesSurvivalSource;
        RaiseManesSurvival();
    }

    internal void RaiseManesSurvival() => manesSurvival.Raise();

    private sealed class ManesSurvivalProxy : IGameManesSurvival
    {
        private static readonly GameManesRanking NoRanking = new(false, Array.Empty<GameManesRankRow>(), default);

        public IGameManesSurvival? Source;
        public event Action? Changed;
        internal void Raise() => Changed?.Invoke();

        public GameManesBoard Board => Source?.Board ?? default;
        public GameManesBar Bar => Source?.Bar ?? default;
        public GameManesOffer? Offer => Source?.Offer;
        public IReadOnlyList<GameManesPassive> Passives => Source?.Passives ?? Array.Empty<GameManesPassive>();
        public GameManesTotals Totals => Source?.Totals ?? default;
        public GameManesChoice? PassiveWon => Source?.PassiveWon;
        public GameManesRanking Ranking => Source?.Ranking ?? NoRanking;
        public void SignUp(bool apply) => Source?.SignUp(apply);
        public void Select(int index) => Source?.Select(index);
        public void Complete() => Source?.Complete();
        public void Buy(int code) => Source?.Buy(code);
        public void Minimise() => Source?.Minimise();
        public void Reopen() => Source?.Reopen();
        public void ClosePassiveWon() => Source?.ClosePassiveWon();
    }
}
