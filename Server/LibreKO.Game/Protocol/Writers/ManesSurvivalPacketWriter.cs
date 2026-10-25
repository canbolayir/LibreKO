using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol.Writers;

public static class ManesSurvivalPacketWriter
{
    public readonly record struct RankRow(byte Nation, short ClanId, string Clan, string Name, int Kills, int Score);

    public readonly record struct BagItem(ItemSlot Slot, PetItemInfo? Pet);

    public static Packet SignUpOpen(int seconds, int participants)
    {
        var packet = Sub(ManesSurvivalSubOpcode.SignUp, (byte)ManesSignUpKind.Open);
        packet.WriteUShort((ushort)Math.Clamp(seconds, 0, ushort.MaxValue));
        packet.WriteShort(Short(participants));
        return packet;
    }

    public static Packet SignUpReply(ManesSignUpResult result, int participants = 0)
    {
        var packet = Sub(ManesSurvivalSubOpcode.SignUp, (byte)ManesSignUpKind.Entry);
        packet.WriteShort((short)result);
        if (result == ManesSignUpResult.Applied)
            packet.WriteUShort((ushort)Math.Clamp(participants, 0, ushort.MaxValue));
        return packet;
    }

    public static Packet Start(byte row, int seconds, long expToLevel, long exp, byte level)
    {
        var packet = Sub(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Start);
        packet.WriteByte(row);
        packet.WriteShort(Short(seconds));
        packet.WriteShort(Short(expToLevel));
        packet.WriteShort(Short(exp));
        packet.WriteByte(level);
        return packet;
    }

    public static Packet Rank(byte myRank, int myKills, int myScore, IReadOnlyList<RankRow> rows)
    {
        var packet = Sub(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Rank);
        packet.WriteByte(myRank);
        packet.WriteShort(Short(myKills));
        packet.WriteUInt(Points(myScore));
        var shown = Math.Min(rows.Count, ManesSurvivalRules.RankRows);
        packet.WriteByte((byte)shown);
        for (int i = 0; i < shown; i++)
        {
            var row = rows[i];
            packet.WriteByte(row.Nation);
            packet.WriteShort(row.ClanId);
            packet.WriteString(row.Clan);
            packet.WriteString(row.Name);
            packet.WriteShort(Short(row.Kills));
            packet.WriteUInt(Points(row.Score));
        }
        return packet;
    }

    public static Packet End(long expToLevel, long exp, byte level, IReadOnlyList<BagItem> bag)
    {
        var packet = Sub(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.End);
        packet.WriteLong(expToLevel);
        packet.WriteLong(exp);
        packet.WriteByte(level);
        for (int i = 0; i < InventoryConstants.HaveMax; i++)
        {
            if (i < bag.Count)
                ItemRecordWriter.Write(packet, bag[i].Slot.ItemId, bag[i].Slot.Durability, (short)bag[i].Slot.Count,
                    bag[i].Slot.Flag, bag[i].Pet, ItemRecordWriter.ExpiryOnWire(bag[i].Slot.ExpiresAt));
            else
                ItemRecordWriter.Write(packet, 0, 0, 0, 0, null);
        }
        return packet;
    }

    public static Packet Boss(string name, int maxHp, int hp, int seconds)
    {
        var packet = Sub(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Boss);
        packet.WriteString(name);
        packet.WriteUInt((uint)Math.Max(0, maxHp));
        packet.WriteUInt((uint)Math.Max(0, hp));
        packet.WriteUInt((uint)Math.Max(0, seconds));
        return packet;
    }

    public static Packet Markers(IReadOnlyList<(int X, int Z)> points)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Markers);
        var shown = Math.Min(points.Count, byte.MaxValue);
        packet.WriteByte((byte)shown);
        for (int i = 0; i < shown; i++)
        {
            packet.WriteShort(Short(points[i].X));
            packet.WriteShort(Short(points[i].Z));
        }
        return packet;
    }

    public static Packet LastBoss(string killer)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.LastBoss);
        packet.WriteString(killer);
        return packet;
    }

    public static Packet Invincible() => Sub(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Invincible);

    public static Packet Passive(short code)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Passive);
        packet.WriteShort(code);
        return packet;
    }

    public static Packet BonusOffer(IReadOnlyList<short> skills)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.BonusOffer);
        WriteCodes(packet, skills);
        return packet;
    }

    public static Packet BonusPickReply(short result)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.BonusChoice);
        packet.WriteShort(result);
        return packet;
    }

    public static Packet Kill(byte killerNation, string killer, byte victimNation, string victim, int points)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Kill);
        packet.WriteByte(killerNation);
        packet.WriteString(killer);
        packet.WriteByte(victimNation);
        packet.WriteString(victim);
        packet.WriteUInt(Points(points));
        return packet;
    }

    public static Packet Reward(byte rank, int totalScore, int itemId, byte count, bool bossKiller)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Reward);
        packet.WriteByte(rank);
        packet.WriteUInt(Points(totalScore));
        packet.WriteInt(itemId);
        packet.WriteByte(count);
        packet.WriteByte(bossKiller ? (byte)1 : (byte)0);
        return packet;
    }

    public static Packet Exp(long exp)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Progress, (byte)ManesProgressKind.Exp);
        packet.WriteByte(0);
        packet.WriteShort(Short(exp));
        return packet;
    }

    public static Packet LevelUp(int characterId, byte level, long expToLevel, long exp, short maxHp, short hp, short maxMp, short mp)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Progress, (byte)ManesProgressKind.Level);
        packet.WriteInt(characterId);
        packet.WriteByte(level);
        packet.WriteShort(Short(expToLevel));
        packet.WriteShort(Short(exp));
        packet.WriteShort(maxHp);
        packet.WriteShort(hp);
        packet.WriteShort(maxMp);
        packet.WriteShort(mp);
        return packet;
    }

    public static Packet Offer(IReadOnlyList<short> skills, IReadOnlyList<short> potions)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Offer);
        WriteCodes(packet, skills);
        WriteCodes(packet, potions);
        return packet;
    }

    public static Packet PickReply(ManesPickAction action, short result)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Choice);
        packet.WriteByte((byte)action);
        packet.WriteShort(result);
        return packet;
    }

    public static Packet BagPut(byte slot, int itemId, int count, short durability, byte flag)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Put);
        packet.WriteByte(slot);
        packet.WriteInt(itemId);
        packet.WriteShort(Short(count));
        packet.WriteShort(durability);
        packet.WriteByte(flag);
        return packet;
    }

    public static Packet DiscardReply(bool removed)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Discard);
        packet.WriteByte(removed ? (byte)1 : (byte)0);
        return packet;
    }

    private static void WriteCodes(Packet packet, IReadOnlyList<short> codes)
    {
        var shown = Math.Min(codes.Count, byte.MaxValue);
        packet.WriteByte((byte)shown);
        for (int i = 0; i < shown; i++)
            packet.WriteShort(codes[i]);
    }

    public static Packet Score(int score)
    {
        var packet = Sub(ManesSurvivalSubOpcode.Progress, (byte)ManesProgressKind.Score);
        packet.WriteUInt(Points(score));
        return packet;
    }

    private static Packet Sub(ManesSurvivalSubOpcode sub, byte kind)
    {
        var packet = new Packet(GameOpcodes.GS_MANES_SURVIVAL);
        packet.WriteByte((byte)sub);
        packet.WriteByte(kind);
        return packet;
    }

    private static short Short(long value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static uint Points(int value) => (uint)Math.Max(0, value);
}
