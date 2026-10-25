using LibreKO.Common.Domain.Entities.GameData;

namespace LibreKO.Game.World;

public sealed class ManesSurvivalArena(byte zone, DateTime startUtc, int durationSeconds)
{
    public byte Zone { get; } = zone;
    public DateTime StartUtc { get; } = startUtc;
    public DateTime EndUtc { get; } = startUtc.AddSeconds(durationSeconds);
    public DateTime BattleOpenUtc { get; } = startUtc.AddSeconds(ManesSurvivalRules.CalmSeconds);
    public List<int> Participants { get; } = [];
    public bool IsCompleted { get; set; }
    public NpcInstance? Boss { get; set; }
    public List<NpcInstance> MidBosses { get; } = [];
    public int NextWave { get; set; }
    public int BossKillerId { get; set; }
    public DateTime? BossKilledUtc { get; set; }
    public int LastBossBarHp { get; set; } = -1;
    public DateTime LastBossBarUtc { get; set; }
    public DateTime LastMarkersUtc { get; set; }
    public bool InvincibilityLifted { get; set; }

    public bool IsBattleOpen(DateTime utcNow) => utcNow >= BattleOpenUtc;

    public int SecondsLeft(DateTime utcNow) => (int)Math.Max(0, Math.Ceiling((EndUtc - utcNow).TotalSeconds));
}

public sealed record ManesSurvivalOffer(IReadOnlyList<short> Skills, IReadOnlyList<short> Potions, bool Bonus);

public sealed class ManesSurvivalEntry(ManesSurvivalArena arena, byte row, ManesSurvivalReturn real)
{
    private readonly object picksLock = new();
    private readonly Dictionary<int, int> groupLevels = [];
    private readonly HashSet<int> skills = [.. ManesSurvivalRules.StartingSkills];
    private readonly Queue<ManesSurvivalOffer> offers = new();

    public ManesSurvivalArena Arena { get; } = arena;
    public byte Row { get; } = row;
    public ManesSurvivalReturn Real { get; } = real;
    public byte Level { get; set; } = ManesSurvivalRules.StartLevel;
    public long Exp { get; set; }
    public int Score { get; set; }
    public int Kills { get; set; }
    public short MaxHp { get; set; }
    public short MaxMp { get; set; }
    public int HpBonus { get; set; }
    public int AttackUp { get; set; }
    public int AttackDown { get; set; }

    public void Gain(ManesSurvivalPick passive)
    {
        HpBonus += passive.Hp;
        AttackUp += passive.AttackUp;
        AttackDown = Math.Min(ManesSurvivalRules.MaxAttackDown, AttackDown + passive.AttackDown);
    }

    public bool Knows(int magicId)
    {
        lock (picksLock)
            return skills.Contains(magicId);
    }

    public int GroupLevel(int group)
    {
        lock (picksLock)
            return groupLevels.GetValueOrDefault(group);
    }

    public void Learn(ManesSurvivalPick pick)
    {
        lock (picksLock)
        {
            groupLevels[pick.Group] = Math.Max(groupLevels.GetValueOrDefault(pick.Group), pick.Level);
            skills.Add(pick.Magic);
        }
    }

    public void Offer(ManesSurvivalOffer offer)
    {
        lock (picksLock)
            offers.Enqueue(offer);
    }

    public ManesSurvivalOffer? CurrentOffer
    {
        get
        {
            lock (picksLock)
                return offers.TryPeek(out var offer) ? offer : null;
        }
    }

    public int PendingOffers
    {
        get
        {
            lock (picksLock)
                return offers.Count;
        }
    }

    public bool CloseOffer(ManesSurvivalOffer offer)
    {
        lock (picksLock)
        {
            if (!offers.TryPeek(out var current) || !ReferenceEquals(current, offer))
                return false;
            offers.Dequeue();
            return true;
        }
    }
}

public sealed record ManesSurvivalReturn(byte Level, long Experience, short Hp, short Mp, ItemSlot[] Bag);
