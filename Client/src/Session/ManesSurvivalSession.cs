namespace LibreKO.Session;

public sealed class ManesSurvivalSession
{
    private readonly SelfSession self;
    private readonly ProgressSession progress;

    public ManesSurvivalSession(SelfSession self, ProgressSession progress)
    {
        this.self = self;
        this.progress = progress;
    }

    public sealed class SignUpBoardState
    {
        public bool Open;
        public double EndsAt;
        public int Participants;
        public bool Applied;
        public bool Collapsed;
    }

    public SignUpBoardState Board { get; } = new();

    public bool Running { get; private set; }

    public int Row { get; private set; }

    public int Score { get; private set; }

    public int[]? Hotbar { get; private set; }

    internal void KeepHotbar(int[] slots) => Hotbar = slots;

    private readonly List<int> passives = new();

    public IReadOnlyList<int> Passives => passives;

    private readonly List<ManesOffer> offers = new();

    public IReadOnlyList<ManesOffer> Offers => offers;

    internal void TakeOffer()
    {
        if (offers.Count > 0) offers.RemoveAt(0);
    }

    public event Action<ManesSignUp>? SignUpEvent;

    internal void RaiseSignUp(ManesSignUp signUp) => SignUpEvent?.Invoke(signUp);

    public event Action<ManesStart>? StartEvent;

    internal void ApplyStart(ManesStart start)
    {
        Running = true;
        Row = start.Row;
        Score = 0;
        Hotbar = null;
        passives.Clear();
        offers.Clear();
        Board.Open = false;
        Board.Applied = false;
        self.Sheet.ApplyLevel(start.Level, self.Sheet.Points, start.Exp, start.MaxExp);
        ClearBag();
        StartEvent?.Invoke(start);
    }

    public event Action<ManesBoss>? BossEvent;

    internal void RaiseBoss(ManesBoss boss) => BossEvent?.Invoke(boss);

    public event Action<ManesRank>? RankEvent;

    internal void RaiseRank(ManesRank rank) => RankEvent?.Invoke(rank);

    public event Action<ManesEnd>? EndEvent;

    internal void ApplyEnd(ManesEnd end)
    {
        Running = false;
        Hotbar = null;
        passives.Clear();
        offers.Clear();
        self.Sheet.ApplyLevel(end.Level, self.Sheet.Points, end.Exp, end.MaxExp);
        ClearBag();
        for (int i = 0; i < end.Bag.Length; i++)
            self.SetInventorySlot(Inventory.GridStart + i, end.Bag[i]);
        EndEvent?.Invoke(end);
    }

    public event Action<IReadOnlyList<ManesMarker>>? MarkersEvent;

    internal void RaiseMarkers(IReadOnlyList<ManesMarker> markers) => MarkersEvent?.Invoke(markers);

    public event Action<string>? LastBossEvent;

    internal void RaiseLastBoss(string name) => LastBossEvent?.Invoke(name);

    public event Action<ManesKill>? KillEvent;

    internal void RaiseKill(ManesKill kill) => KillEvent?.Invoke(kill);

    public event Action<ManesReward>? RewardEvent;

    internal void RaiseReward(ManesReward reward) => RewardEvent?.Invoke(reward);

    public event Action? InvincibleEvent;

    internal void RaiseInvincible() => InvincibleEvent?.Invoke();

    internal void ApplyExp(int exp) => progress.ApplyExp(exp);

    internal void ApplyLevel(ManesLevel level)
    {
        if (level.CharId == self.MyCharId)
            progress.ApplyLevel(level.Level, self.Sheet.Points, self.Mastery.Pool, level.MaxExp, level.Exp, level.MaxHp, level.Hp, level.MaxMp, level.Mp);
        else
            progress.RaisePeerLevelChange(level.CharId, level.Level);
    }

    public event Action<int>? ScoreEvent;

    internal void ApplyScore(int score)
    {
        Score = score;
        ScoreEvent?.Invoke(score);
    }

    public event Action<ManesLoot>? LootEvent;

    internal void RaiseLoot(ManesLoot loot) => LootEvent?.Invoke(loot);

    public event Action<ManesBagItem>? BagItemEvent;

    internal void ApplyBagItem(ManesBagItem item)
    {
        if (item.Slot < 0 || item.Slot >= ManesSurvival.BagSlots) return;
        int abs = Inventory.GridStart + item.Slot;
        var held = self.Inventory[abs];
        if (held.ItemId == item.ItemId) held.Count = (short)item.Count;
        else held = new ItemSlot { ItemId = item.ItemId, Count = (short)item.Count, Durability = (short)item.Durability, Flag = (byte)item.Flag };
        self.SetInventorySlot(abs, held);
        BagItemEvent?.Invoke(item);
    }

    public event Action<bool>? BagReplyEvent;

    internal void RaiseBagReply(bool done) => BagReplyEvent?.Invoke(done);

    public event Action<ManesOffer>? OfferEvent;

    internal void ApplyOffer(ManesOffer offer)
    {
        if (offer.Skills.Count == 0) return;
        offers.Add(offer);
        OfferEvent?.Invoke(offer);
    }

    public event Action<ManesPickReply>? PickReplyEvent;

    internal void RaisePickReply(ManesPickReply reply) => PickReplyEvent?.Invoke(reply);

    public event Action<int>? PassiveEvent;

    internal void ApplyPassive(int code)
    {
        passives.Add(code);
        PassiveEvent?.Invoke(code);
    }

    internal void KeepPassive(int code) => passives.Add(code);

    private void ClearBag()
    {
        for (int i = 0; i < ManesSurvival.BagSlots; i++)
            self.SetInventorySlot(Inventory.GridStart + i, default);
    }

    internal void Reset()
    {
        Running = false;
        Score = 0;
        Hotbar = null;
        passives.Clear();
        offers.Clear();
        Board.Open = false;
        Board.Applied = false;
    }
}
