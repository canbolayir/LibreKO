using LibreKO.Common.Enums;
using LibreKO.Game.Configuration;

namespace LibreKO.Game.World;

public static class ManesSurvivalRules
{
    public const byte FirstZone = (byte)ZoneId.ManesSurvival1;
    public const byte LastZone = (byte)ZoneId.ManesSurvival4;
    public const int ArenaCount = LastZone - FirstZone + 1;
    public const int DurationSeconds = 1200;
    public const int CalmSeconds = 180;
    public const int BossInvincibleSeconds = 600;
    public const byte StartLevel = 1;
    public const byte MaxLevel = 30;
    public const byte DefaultMinLevel = 30;
    public const int RankRows = 20;
    public const byte Unranked = 0;
    public const short SkillTree = ChaosRules.SealedZoneSkillTree;
    public const int OfferSize = 4;
    public const int LevelsPerPotionTier = 10;

    public const int Sprint = 491200;
    public const int Stroke = 491201;
    public const int HpPotionMagic = 491410;
    public const int MpPotionMagic = 491411;
    public const int EyesOfManes = 491412;
    public const int HpPotionItem = 978023000;
    public const int MpPotionItem = 978024000;
    public const int MidBossTicketItem = 978025000;
    public static readonly int[] StartingSkills = [Sprint, Stroke, HpPotionMagic, MpPotionMagic, EyesOfManes];

    public const int MaxAttackDown = 80;
    public const int MarkerSeconds = 30;
    public const int BossBarSeconds = 1;
    public const int InvincibleNoticeSeconds = 5;
    public static readonly (int X, int Z) BossSpot = (512, 512);
    public static readonly (int X, int Z)[] MidBossSpots =
    [
        (280, 655), (650, 669), (731, 484), (520, 232), (650, 353), (488, 765), (477, 257), (297, 540), (274, 439), (599, 703),
    ];

    public const short PickSucceeded = 1;
    public const short PickFailed = -1;
    public const short PickBagFull = -10;
    public const short PickNotEnoughNoah = -11;

    public const byte KarusWarriorRow = 1;
    public const byte KarusPriestRow = 2;
    public const byte ElMoradWarriorRow = 3;
    public const byte ElMoradPriestRow = 4;

    public readonly record struct Score(int CharacterId, int Points, int Kills);

    public static bool IsZone(byte zoneId) => zoneId is >= FirstZone and <= LastZone;

    public static byte ArenaZone(int index) => (byte)(FirstZone + index);

    public static int ArenasFor(int players, int playersPerArena) =>
        players <= 0 ? 0 : Math.Min(ArenaCount, (players + Math.Max(1, playersPerArena) - 1) / Math.Max(1, playersPerArena));

    public static int Capacity(int playersPerArena) => ArenaCount * Math.Max(1, playersPerArena);

    public static byte RowFor(AccountNation nation, short classId)
    {
        bool caster = ClassIdHelper.IsPriest(classId) || ClassIdHelper.IsMage(classId);
        return nation == AccountNation.Karus
            ? caster ? KarusPriestRow : KarusWarriorRow
            : caster ? ElMoradPriestRow : ElMoradWarriorRow;
    }

    public static short MaxHp(ManesSurvivalSettings settings, byte level, int bonus) =>
        (short)Math.Clamp(settings.StartHp + settings.HpPerLevel * (level - StartLevel) + bonus, 1, short.MaxValue);

    public static short MaxMp(ManesSurvivalSettings settings, byte level) =>
        (short)Math.Clamp(settings.StartMp + settings.MpPerLevel * (level - StartLevel), 0, short.MaxValue);

    public static long ExpToLevel(ManesSurvivalSettings settings, byte level) =>
        Math.Max(1, settings.ExpToSecondLevel + (long)settings.ExpPerLevel * (level - StartLevel));

    public static bool IsEventItem(int itemId) => itemId is HpPotionItem or MpPotionItem or MidBossTicketItem;

    public static bool SkillAllowed(byte zoneId, short skillTree) => !IsZone(zoneId) || skillTree == SkillTree;

    public static int PotionTier(byte level) => Math.Max(0, (level - StartLevel) / LevelsPerPotionTier);

    public static (byte Level, long Exp, int Levels) Gain(ManesSurvivalSettings settings, byte level, long exp, long gained)
    {
        int levels = 0;
        exp += Math.Max(0, gained);
        while (level < MaxLevel && exp >= ExpToLevel(settings, level))
        {
            exp -= ExpToLevel(settings, level);
            level++;
            levels++;
        }
        return (level, level >= MaxLevel ? 0 : exp, levels);
    }

    public static bool Hostile(UserSession attacker, UserSession target, DateTime utcNow) =>
        attacker.ManesSurvival is { } mine && target.ManesSurvival is { } theirs
            && ReferenceEquals(mine.Arena, theirs.Arena) && mine.Arena.IsBattleOpen(utcNow);

    public static IReadOnlyList<Score> Ordered(IEnumerable<Score> scores) =>
        scores.OrderByDescending(s => s.Points).ThenByDescending(s => s.Kills).ThenBy(s => s.CharacterId).ToList();

    public static byte RankOf(IReadOnlyList<Score> ordered, int characterId)
    {
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i].CharacterId == characterId)
                return ordered[i].Points > 0 ? (byte)Math.Min(i + 1, byte.MaxValue) : Unranked;
        return Unranked;
    }

    public static ManesSurvivalReward? RewardFor(IEnumerable<ManesSurvivalReward> rewards, byte rank) =>
        rank == Unranked ? null : rewards.FirstOrDefault(r => rank >= r.FromRank && rank <= r.ToRank && r.ItemId > 0);
}
