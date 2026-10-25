namespace LibreKO.Network;

public enum AdminRequestSubOpcode : byte
{
    State = 1,
    Coins = 2,
    Stats = 3,
    GiveItem = 4,
    SetClass = 5,
    Zone = 6,
    ItemSearch = 7,
    CollectionRaces = 8,
    CollectionRaceStart = 9,
    CollectionRaceClose = 10,
    SetLevel = 11,
    SetSkill = 12,
    SetLook = 13,
    Find = 14,
    Go = 15,
    SpawnRow = 16,
    SpawnSet = 17,
    SpawnPersist = 18,
    Cash = 19,
    Commands = 20,
    Monster = 21,
    MonsterSet = 22,
    MonsterPersist = 23,
    DropsSet = 24,
    DropsPersist = 25,
    ItemGroup = 26,
    ItemGroupSet = 27,
    ItemGroupPersist = 28,
    DropSimulate = 29,
    TargetSpawnRow = 30,
    EventSchedules = 31,
    EventRuleSave = 32,
    EventRuleDelete = 33,
    Lottery = 34,
    LotterySave = 35,
    LotteryDelete = 36,
    Race = 37,
    RaceSave = 38,
    RaceDelete = 39,
    Tips = 40,
    TipSave = 41,
    TipDelete = 42,
    TipSettings = 43,
    EventDurationSave = 44,
    PlayerSheet = 45,
    InventoryEdit = 46,
    ItemGroups = 47,
}

public enum AdminInventoryEdit : byte
{
    Put = 1,
    Delete = 2,
    Give = 3,
}

public enum AdminReplySubOpcode : byte
{
    State = 0x10,
    Result = 0x11,
    Grant = 0x12,
    GmFx = 0x13,
    CollectionRaces = 0x14,
    Find = 0x15,
    SpawnRow = 0x16,
    Commands = 0x17,
    Monster = 0x18,
    ItemGroup = 0x19,
    DropSimulation = 0x1A,
    EventSchedules = 0x1B,
    Lottery = 0x1C,
    Race = 0x1D,
    Tips = 0x1E,
    PlayerSheet = 0x1F,
    ItemGroups = 0x20,
}

public enum AdminMonsterBy : byte
{
    Target = 0,
    Monster = 1,
    Npc = 2,
}

public enum AdminProgressMode : byte
{
    Keep = 0,
    Reset = 1,
}

public enum AdminFindKind : byte
{
    Npcs = 0,
    Monsters = 1,
    Players = 2,
}

public static class AdminWire
{
    public static (List<LibreKO.Domain.GmCommandInfo> Commands, bool Open) ReadCommands(Packet p)
    {
        var commands = new List<LibreKO.Domain.GmCommandInfo>();
        if (p.RemainingBytes < 3) return (commands, false);
        bool open = p.ReadByte() != 0;
        int count = p.ReadShort();
        for (int i = 0; i < count && p.RemainingBytes >= 6; i++)
            commands.Add(new LibreKO.Domain.GmCommandInfo(p.ReadString(), p.ReadString(), p.ReadString()));
        return (commands, open);
    }

    public static AdminMonsterSheet ReadMonsterSheet(Packet p)
    {
        var sheet = new AdminMonsterSheet
        {
            IsMonster = p.ReadByte() == 1,
            ProtoId = p.ReadInt(),
            Name = p.ReadSByteString(),
            NpcType = p.ReadByte(),
            SpawnRow = p.ReadInt(),
            Alive = p.ReadInt(),
        };
        int fields = p.ReadByte();
        for (int i = 0; i < fields; i++)
            sheet.Fields.Add(new AdminMonsterField(p.ReadByte(), p.ReadSByteString(), p.ReadInt(), p.ReadInt(), p.ReadInt()));
        sheet.DropRow = p.ReadShort();
        sheet.DropRowExists = p.ReadByte() == 1;
        sheet.DropRowSharedBy = p.ReadUShort();
        int drops = p.ReadByte();
        for (int i = 0; i < drops; i++)
            sheet.Drops.Add(new AdminDropSlot(p.ReadInt(), p.ReadShort(), p.ReadSByteString()));
        sheet.DropEventPercent = p.ReadByte();
        sheet.CoinEventPercent = p.ReadByte();
        return sheet;
    }

    private const byte EditableFlag = 1;
    private const byte FromSettingsFlag = 2;
    private const byte HasLevelsFlag = 4;
    private const byte NoWeekday = byte.MaxValue;

    public static AdminEventSchedules ReadEventSchedules(Packet p)
    {
        string zone = p.ReadSByteString();
        int offset = p.ReadInt();
        int count = p.ReadUShort();
        var owners = new List<LibreKO.Domain.EventOwner>(count);
        for (int i = 0; i < count; i++)
        {
            var kind = (LibreKO.Domain.EventKind)p.ReadByte();
            int id = p.ReadInt();
            string name = p.ReadSByteString();
            var clock = (LibreKO.Domain.EventRuleClock)p.ReadByte();
            int flags = p.ReadByte();
            int duration = p.ReadUShort();
            int ruleCount = p.ReadUShort();
            var rules = new List<LibreKO.Domain.EventRule>(ruleCount);
            for (int r = 0; r < ruleCount; r++)
            {
                int ruleId = p.ReadInt();
                var repeat = (LibreKO.Domain.ScheduleRepeat)p.ReadByte();
                var date = LibreKO.Domain.EventCalendar.DateFromNumber(p.ReadInt());
                int weekday = p.ReadByte();
                rules.Add(new LibreKO.Domain.EventRule(ruleId, repeat, date, weekday == NoWeekday ? null : (DayOfWeek)weekday,
                    p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte()));
            }
            owners.Add(new LibreKO.Domain.EventOwner(kind, id, name, clock, (flags & EditableFlag) != 0, (flags & FromSettingsFlag) != 0,
                (flags & HasLevelsFlag) != 0, rules, duration));
        }
        return new AdminEventSchedules(zone, offset, owners);
    }

    public static AdminLottery ReadLottery(Packet p)
    {
        var lottery = new AdminLottery
        {
            Id = p.ReadInt(),
            Name = p.ReadSByteString(),
            DurationMinutes = p.ReadInt(),
            UserLimit = p.ReadInt(),
            ReqItemId = p.ReadInt(),
            ReqItemCount = p.ReadInt(),
            AutoStart = p.ReadByte() == 1,
            Running = p.ReadByte() == 1,
        };
        int count = p.ReadByte();
        for (int i = 0; i < count; i++)
            lottery.Rewards.Add(new AdminLotteryReward(p.ReadInt(), p.ReadInt(), p.ReadInt()));
        return lottery;
    }

    public static AdminItemGroups ReadItemGroups(Packet p)
    {
        var rows = new List<AdminItemGroupRow>();
        if (p.RemainingBytes < sizeof(ushort) + 1) return new AdminItemGroups(0, rows);
        int total = p.ReadUShort();
        int count = p.ReadByte();
        for (int i = 0; i < count && p.RemainingBytes >= sizeof(int) + sizeof(ushort) + 1; i++)
        {
            int group = p.ReadInt();
            int items = p.ReadUShort();
            int samples = p.ReadByte();
            var sample = new int[samples];
            for (int s = 0; s < samples && p.RemainingBytes >= sizeof(int); s++) sample[s] = p.ReadInt();
            rows.Add(new AdminItemGroupRow(group, items, sample));
        }
        return new AdminItemGroups(total, rows);
    }

    public static DateTime? FromUnixSeconds(uint seconds) =>
        seconds == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

    public static AdminPlayerSheet ReadPlayerSheet(Packet p)
    {
        var sheet = new AdminPlayerSheet();
        if (p.RemainingBytes < sizeof(int) + 1) return sheet;
        sheet.Id = p.ReadInt();
        sheet.Name = p.ReadSByteString();
        sheet.Online = p.ReadByte() == 1;
        sheet.Level = p.ReadByte();
        sheet.Class = p.ReadShort();
        sheet.Nation = p.ReadByte();
        sheet.Zone = p.ReadByte();
        sheet.X = p.ReadUShort();
        sheet.Z = p.ReadUShort();
        sheet.Gold = p.ReadInt();
        sheet.KnightCash = p.ReadInt();
        sheet.Loyalty = p.ReadInt();
        sheet.LastSeen = FromUnixSeconds(p.ReadUInt());
        int count = p.ReadByte();
        for (int i = 0; i < count && p.RemainingBytes >= SheetSlotBytes; i++)
            sheet.Slots.Add(new AdminSheetSlot(p.ReadByte(), p.ReadInt(), p.ReadUShort(), p.ReadShort(), p.ReadByte()));
        return sheet;
    }

    private const int SheetSlotBytes = 1 + sizeof(int) + sizeof(ushort) + sizeof(short) + 1;

    public static AdminTips ReadTips(Packet p)
    {
        var tips = new AdminTips { Enabled = p.ReadByte() == 1, IntervalMinutes = p.ReadUShort() };
        int count = p.ReadUShort();
        for (int i = 0; i < count; i++)
            tips.Tips.Add(new AdminTip(p.ReadInt(), (GameLanguage)p.ReadByte(), p.ReadByte() == 1, p.ReadString()));
        return tips;
    }

    public static AdminRace ReadRace(Packet p)
    {
        var race = new AdminRace
        {
            Id = p.ReadInt(),
            Name = p.ReadSByteString(),
            ZoneId = p.ReadByte(),
            MinLevel = p.ReadByte(),
            MaxLevel = p.ReadByte(),
            DurationMinutes = p.ReadInt(),
            MaxWinners = p.ReadInt(),
            AutoStart = p.ReadByte() == 1,
            Running = p.ReadByte() == 1,
        };
        int objectives = p.ReadByte();
        for (int i = 0; i < objectives; i++)
            race.Objectives.Add(new AdminRaceObjective((AdminRaceTarget)p.ReadByte(), p.ReadInt(), p.ReadInt(), p.ReadSByteString()));
        int rewards = p.ReadByte();
        for (int i = 0; i < rewards; i++)
            race.Rewards.Add(new AdminRaceReward(p.ReadInt(), p.ReadInt(), p.ReadByte()));
        return race;
    }

    public static AdminItemGroup ReadItemGroup(Packet p)
    {
        int group = p.ReadInt();
        bool exists = p.ReadByte() == 1;
        var items = new int[p.ReadUShort()];
        for (int i = 0; i < items.Length; i++)
            items[i] = p.ReadInt();
        return new AdminItemGroup(group, exists, items);
    }

    public static AdminDropSimulation ReadDropSimulation(Packet p)
    {
        var result = new AdminDropSimulation
        {
            IsMonster = p.ReadByte() == 1,
            ProtoId = p.ReadInt(),
            Rolls = p.ReadInt(),
            CoinTotal = p.ReadLong(),
            CoinMin = p.ReadInt(),
            CoinMax = p.ReadInt(),
            ItemlessKills = p.ReadInt(),
        };
        int count = p.ReadUShort();
        for (int i = 0; i < count; i++)
            result.Items.Add((p.ReadInt(), p.ReadInt()));
        return result;
    }
}

public sealed record AdminMonsterField(int Id, string Label, int Value, int Min, int Max);

public sealed record AdminDropSlot(int Value, int Percent, string Label)
{
    public const int FirstGroupId = 100;
    public const int FirstItemId = 100_000_000;

    public static bool IsGroup(int value) => value is >= FirstGroupId and < FirstItemId;
}

public sealed class AdminMonsterSheet
{
    public bool IsMonster;
    public int ProtoId;
    public string Name = string.Empty;
    public int NpcType;
    public int SpawnRow;
    public int Alive;
    public List<AdminMonsterField> Fields = new();
    public int DropRow;
    public bool DropRowExists;
    public int DropRowSharedBy;
    public List<AdminDropSlot> Drops = new();
    public int DropEventPercent;
    public int CoinEventPercent;
}

public sealed record AdminItemGroup(int Group, bool Exists, int[] Items);

public sealed record AdminItemGroupRow(int Group, int ItemCount, int[] Samples);

public sealed record AdminItemGroups(int Total, List<AdminItemGroupRow> Rows);

public sealed record AdminTip(int Id, GameLanguage Language, bool Enabled, string Text);

public sealed class AdminTips
{
    public const int MaxTextLength = 300;
    public const int MaxIntervalMinutes = 720;

    public bool Enabled = true;
    public int IntervalMinutes = 5;
    public List<AdminTip> Tips = new();
}

public sealed record AdminLotteryReward(int Place, int ItemId, int Count);

public sealed class AdminLottery
{
    public int Id;
    public string Name = string.Empty;
    public int DurationMinutes = 15;
    public int UserLimit = 100;
    public int ReqItemId = LibreKO.Domain.QuestData.CoinItemId;
    public int ReqItemCount = 100_000;
    public bool AutoStart;
    public bool Running;
    public List<AdminLotteryReward> Rewards = new();
}

public enum AdminRaceTarget : byte { Monster = 0, EnemyPlayer = 1, Item = 2 }

public sealed record AdminRaceObjective(AdminRaceTarget Kind, int TargetId, int Count, string Label);

public sealed record AdminRaceReward(int ItemId, int Count, int Rate);

public sealed class AdminRace
{
    public int Id;
    public string Name = string.Empty;
    public int ZoneId = 21;
    public int MinLevel = 1;
    public int MaxLevel = 83;
    public int DurationMinutes = 60;
    public int MaxWinners = 16;
    public bool AutoStart;
    public bool Running;
    public List<AdminRaceObjective> Objectives = new();
    public List<AdminRaceReward> Rewards = new();
}

public sealed record AdminEventSchedules(string ZoneId, int OffsetMinutes, List<LibreKO.Domain.EventOwner> Owners);

public sealed class AdminDropSimulation
{
    public bool IsMonster;
    public int ProtoId;
    public int Rolls;
    public long CoinTotal;
    public int CoinMin;
    public int CoinMax;
    public int ItemlessKills;
    public List<(int ItemId, int Times)> Items = new();
}

public class AdminCollectionRace
{
    public int Id;
    public string Name = string.Empty;
    public byte ZoneId;
    public byte MinLevel;
    public byte MaxLevel;
    public int DurationMinutes;
    public bool AutoStart;
    public bool Active;
    public int RemainingSeconds;
    public int Completions;
    public int MaxWinners;
    public string Schedule = string.Empty;
    public string Objectives = string.Empty;
}

public class AdminFindHit
{
    public int Id;
    public int SpawnRow;
    public string Name = string.Empty;
    public int Level;
    public int Zone;
    public int X;
    public int Z;
    public bool Monster;
    public bool Bot;
    public bool Online;
    public int Class;
    public int Nation;
    public DateTime? LastSeen;
}

public readonly record struct AdminSheetSlot(int Position, int ItemId, int Count, int Durability, int Flag);

public sealed class AdminPlayerSheet
{
    public int Id;
    public string Name = string.Empty;
    public bool Online;
    public int Level;
    public int Class;
    public int Nation;
    public int Zone;
    public int X;
    public int Z;
    public int Gold;
    public int KnightCash;
    public int Loyalty;
    public DateTime? LastSeen;
    public List<AdminSheetSlot> Slots = new();

    public AdminSheetSlot? At(int position) => Slots.FirstOrDefault(slot => slot.Position == position) is { ItemId: > 0 } found ? found : null;
}

public class AdminSpawnRow
{
    public bool CanPersist;
    public int Index;
    public int NpcId;
    public string Name = string.Empty;
    public int Zone;
    public bool Monster;
    public int X;
    public int Z;
    public float Y;
    public int Direction;
    public int Count;
    public int RespawnSeconds;
    public int SpawnRange;
    public int Alive;
}

public enum AdminPanelGrant : byte
{
    None = 0,
    GameMaster = 1,
    PublicDemo = 2,
}
