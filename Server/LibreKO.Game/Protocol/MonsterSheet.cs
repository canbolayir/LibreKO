using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public sealed record MonsterField(
    byte Id, string Label, int Min, int Max, Func<NpcData, int> Read, Action<NpcData, int> Write, Action<NpcInstance, int>? Live = null);

public static class MonsterSheet
{
    private const int ShortMax = short.MaxValue;
    private const int ByteMax = byte.MaxValue;

    public static readonly IReadOnlyList<MonsterField> Fields =
    [
        new(1, "Level", 1, ByteMax, n => n.Level, (n, v) => n.Level = (short)v, (i, v) => i.Level = (short)v),
        new(2, "HP", 1, int.MaxValue, n => n.Hp, (n, v) => n.Hp = v, (i, v) => i.WithLock(live => { live.MaxHp = v; live.Hp = Math.Min(live.Hp, v); })),
        new(3, "MP", 0, ShortMax, n => n.Mp, (n, v) => n.Mp = (short)v, (i, v) => i.WithLock(live => { live.MaxMp = v; live.Mp = Math.Min(live.Mp, v); })),
        new(4, "Defence", 0, ShortMax, n => n.Ac, (n, v) => n.Ac = (short)v, (i, v) => i.Ac = (short)v),
        new(5, "Attack", 0, ShortMax, n => n.Attack1, (n, v) => n.Attack1 = (short)v, (i, v) => i.Attack1 = (short)v),
        new(6, "Attack 2", 0, ShortMax, n => n.Attack2, (n, v) => n.Attack2 = (short)v, (i, v) => i.Attack2 = (short)v),
        new(7, "Experience", 0, int.MaxValue, n => n.Experience, (n, v) => n.Experience = v, (i, v) => i.Experience = v),
        new(8, "National points", 0, int.MaxValue, n => n.Loyalty, (n, v) => n.Loyalty = v),
        new(9, "Gold", 0, int.MaxValue, n => n.Money, (n, v) => n.Money = v, (i, v) => i.GoldDrop = v),
        new(10, "Drop row", 0, ShortMax, n => n.ItemGroup, (n, v) => n.ItemGroup = (short)v, (i, v) => i.DropItemGroup = (short)v),
        new(11, "Hit rate", 0, ShortMax, n => n.HitRate, (n, v) => n.HitRate = (short)v, (i, v) => i.HitRate = (short)v),
        new(12, "Evade rate", 0, ShortMax, n => n.EvadeRate, (n, v) => n.EvadeRate = (short)v, (i, v) => i.EvadeRate = (short)v),
        new(13, "Attack delay ms", 0, ShortMax, n => n.AttackDelay, (n, v) => n.AttackDelay = (short)v),
        new(14, "Walk speed", 0, ByteMax, n => n.Speed1, (n, v) => n.Speed1 = (byte)v),
        new(15, "Run speed", 0, ByteMax, n => n.Speed2, (n, v) => n.Speed2 = (byte)v),
        new(16, "Attack range", 0, ByteMax, n => n.AttackRange, (n, v) => n.AttackRange = (byte)v),
        new(17, "Search range", 0, ByteMax, n => n.SearchRange, (n, v) => n.SearchRange = (byte)v),
        new(18, "Tracing range", 0, ByteMax, n => n.TracingRange, (n, v) => n.TracingRange = (byte)v),
        new(19, "Fire resist", 0, ShortMax, n => n.FireR, (n, v) => n.FireR = (short)v, (i, v) => i.FireR = (short)v),
        new(20, "Ice resist", 0, ShortMax, n => n.ColdR, (n, v) => n.ColdR = (short)v, (i, v) => i.ColdR = (short)v),
        new(21, "Lightning resist", 0, ShortMax, n => n.LightningR, (n, v) => n.LightningR = (short)v, (i, v) => i.LightningR = (short)v),
        new(22, "Magic resist", 0, ShortMax, n => n.MagicR, (n, v) => n.MagicR = (short)v, (i, v) => i.MagicR = (short)v),
        new(23, "Poison resist", 0, ShortMax, n => n.PoisonR, (n, v) => n.PoisonR = (short)v, (i, v) => i.PoisonR = (short)v),
        new(24, "Curse resist", 0, ShortMax, n => n.CurseR, (n, v) => n.CurseR = (short)v, (i, v) => i.CurseR = (short)v),
    ];

    public static MonsterField? Find(byte id) => Fields.FirstOrDefault(field => field.Id == id);

    public static void CopyTo(NpcData from, NpcData to)
    {
        foreach (var field in Fields)
            field.Write(to, field.Read(from));
    }
}
