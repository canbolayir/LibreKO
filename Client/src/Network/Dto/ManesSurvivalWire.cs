namespace LibreKO.Network;

public enum ManesSurvivalSubOpcode : byte
{
    SignUp = 1,
    State = 2,
    Notice = 3,
    Progress = 4,
    Bag = 5,
    Pick = 6,
}

public enum ManesSignUpKind : byte
{
    Open = 1,
    Entry = 2,
}

public enum ManesSignUpAction : ushort
{
    Apply = 1,
    Cancel = 2,
}

public enum ManesStateKind : byte
{
    Start = 1,
    Boss = 2,
    Rank = 3,
    End = 4,
}

public enum ManesNoticeKind : byte
{
    Markers = 1,
    LastBoss = 2,
    Kill = 3,
    Reward = 4,
    Invincible = 5,
}

public enum ManesProgressKind : byte
{
    Exp = 1,
    Level = 2,
    Score = 3,
}

public enum ManesBagKind : byte
{
    Loot = 1,
    Put = 2,
    Discard = 3,
}

public enum ManesPickKind : byte
{
    Offer = 1,
    Choice = 2,
    Passive = 3,
    BonusOffer = 4,
    BonusChoice = 5,
}

public enum ManesPickAction : byte
{
    Skill = 1,
    Potion = 2,
    Skip = 3,
}

public readonly record struct ManesSignUp(ManesSignUpKind Kind, int Seconds, int Participants, int Result);

public readonly record struct ManesStart(int Row, int Seconds, long MaxExp, long Exp, int Level);

public readonly record struct ManesBoss(string Name, long MaxHp, long Hp, int Seconds);

public sealed record ManesRankRow(int Nation, int ClanId, string Clan, string Name, int Kills, int Score);

public sealed record ManesRank(int MyRank, int MyKills, int MyScore, IReadOnlyList<ManesRankRow> Rows);

public sealed record ManesEnd(long MaxExp, long Exp, int Level, ItemSlot[] Bag);

public readonly record struct ManesKill(int KillerNation, string Killer, int VictimNation, string Victim, long Points);

public readonly record struct ManesReward(int Rank, long TotalScore, int ItemId, int Count, bool BossKiller);

public readonly record struct ManesLevel(int CharId, int Level, int MaxExp, int Exp, int MaxHp, int Hp, int MaxMp, int Mp);

public readonly record struct ManesLoot(int BundleId, int Position, int ItemId, int Count);

public readonly record struct ManesBagItem(int Slot, int ItemId, int Count, int Durability, int Flag);

public sealed record ManesOffer(IReadOnlyList<int> Skills, IReadOnlyList<int> Potions, bool Bonus);

public readonly record struct ManesPickReply(ManesPickAction Action, int Result, bool Bonus);

public readonly record struct ManesMarker(int X, int Z);

public static class ManesSurvivalWire
{
    public const int SignUpApplied = 1;
    public const int SignUpCancelled = 2;
    public const int PickSucceeded = 1;
    public const int UnknownBundleSlot = -1;

    public static ManesSignUp ReadSignUp(Packet p)
    {
        if (p.RemainingBytes < 1) return default;
        var kind = (ManesSignUpKind)p.ReadByte();
        if (kind == ManesSignUpKind.Open)
        {
            if (p.RemainingBytes < 4) return new ManesSignUp(kind, 0, 0, 0);
            int seconds = p.ReadUShort();
            int participants = p.ReadShort();
            return new ManesSignUp(kind, seconds, participants, 0);
        }
        if (p.RemainingBytes < 2) return new ManesSignUp(kind, 0, 0, 0);
        int result = p.ReadShort();
        int count = result == SignUpApplied && p.RemainingBytes >= 2 ? p.ReadUShort() : 0;
        return new ManesSignUp(kind, 0, count, result);
    }

    public static ManesStart ReadStart(Packet p) => p.RemainingBytes >= 8
        ? new ManesStart(p.ReadByte(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadByte())
        : default;

    public static ManesBoss ReadBoss(Packet p)
    {
        if (p.RemainingBytes < 2) return default;
        string name = p.ReadString();
        if (p.RemainingBytes < 12) return new ManesBoss(name, 0, 0, 0);
        return new ManesBoss(name, p.ReadUInt(), p.ReadUInt(), (int)p.ReadUInt());
    }

    public static ManesRank ReadRank(Packet p)
    {
        if (p.RemainingBytes < 8) return new ManesRank(0, 0, 0, []);
        int myRank = p.ReadByte();
        int myKills = p.ReadShort();
        int myScore = p.ReadInt();
        int count = Math.Min((int)p.ReadByte(), ManesSurvival.RankRows);
        var rows = new List<ManesRankRow>(count);
        for (int i = 0; i < count && p.RemainingBytes >= 13; i++)
        {
            int nation = p.ReadByte();
            int clanId = p.ReadShort();
            string clan = p.ReadString();
            string name = p.ReadString();
            if (p.RemainingBytes < 6) break;
            rows.Add(new ManesRankRow(nation, clanId, clan, name, p.ReadShort(), p.ReadInt()));
        }
        return new ManesRank(myRank, myKills, myScore, rows);
    }

    public static ManesEnd ReadEnd(Packet p, Action<PetItemInfo?> remember)
    {
        if (p.RemainingBytes < 17) return new ManesEnd(0, 0, 0, []);
        long maxExp = p.ReadLong();
        long exp = p.ReadLong();
        int level = p.ReadByte();
        var bag = new List<ItemSlot>(ManesSurvival.BagSlots);
        for (int i = 0; i < ManesSurvival.BagSlots && p.RemainingBytes >= MyInfoWire.SlotBytes; i++)
        {
            bag.Add(PetWire.ReadItemRecord(p, out var pet));
            remember(pet);
        }
        return new ManesEnd(maxExp, exp, level, bag.ToArray());
    }

    public static IReadOnlyList<ManesMarker> ReadMarkers(Packet p)
    {
        if (p.RemainingBytes < 1) return [];
        int count = p.ReadByte();
        var markers = new List<ManesMarker>(count);
        for (int i = 0; i < count && p.RemainingBytes >= 4; i++)
            markers.Add(new ManesMarker(p.ReadShort(), p.ReadShort()));
        return markers;
    }

    public static ManesKill ReadKill(Packet p)
    {
        if (p.RemainingBytes < 3) return default;
        int killerNation = p.ReadByte();
        string killer = p.ReadString();
        if (p.RemainingBytes < 3) return default;
        int victimNation = p.ReadByte();
        string victim = p.ReadString();
        long points = p.RemainingBytes >= 4 ? p.ReadUInt() : 0;
        return new ManesKill(killerNation, killer, victimNation, victim, points);
    }

    public static ManesReward ReadReward(Packet p) => p.RemainingBytes >= 11
        ? new ManesReward(p.ReadByte(), p.ReadUInt(), p.ReadInt(), p.ReadByte(), p.ReadByte() != 0)
        : default;

    public static int ReadExp(Packet p)
    {
        if (p.RemainingBytes < 3) return 0;
        p.ReadByte();
        return p.ReadShort();
    }

    public static ManesLevel ReadLevel(Packet p) => p.RemainingBytes >= 17
        ? new ManesLevel(p.ReadInt(), p.ReadByte(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadShort())
        : default;

    public static ManesLoot ReadLoot(Packet p) => p.RemainingBytes >= 12
        ? new ManesLoot(p.ReadInt(), p.ReadShort(), p.ReadInt(), p.ReadShort())
        : default;

    public static ManesBagItem ReadBagItem(Packet p) => p.RemainingBytes >= 10
        ? new ManesBagItem(p.ReadByte(), p.ReadInt(), p.ReadShort(), p.ReadShort(), p.ReadByte())
        : new ManesBagItem(-1, 0, 0, 0, 0);

    public static ManesOffer ReadOffer(Packet p, bool bonus)
    {
        var skills = ReadCodes(p);
        var potions = bonus ? new List<int>() : ReadCodes(p);
        return new ManesOffer(skills, potions, bonus);
    }

    private static List<int> ReadCodes(Packet p)
    {
        if (p.RemainingBytes < 1) return [];
        int count = p.ReadByte();
        var codes = new List<int>(count);
        for (int i = 0; i < count && p.RemainingBytes >= 2; i++)
            codes.Add(p.ReadShort());
        return codes;
    }

    public static ManesPickReply ReadPickReply(Packet p, bool bonus)
    {
        var action = ManesPickAction.Skill;
        if (!bonus)
        {
            if (p.RemainingBytes < 1) return default;
            action = (ManesPickAction)p.ReadByte();
        }
        int result = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
        return new ManesPickReply(action, result, bonus);
    }
}
