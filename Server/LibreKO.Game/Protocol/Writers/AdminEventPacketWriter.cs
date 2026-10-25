using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class AdminEventPacketWriter
{
    public const byte ScheduleClock = 0;
    public const byte UtcClock = 1;
    public const byte Editable = 1;
    public const byte FromSettings = 2;
    public const byte HasLevels = 4;
    public const byte NoWeekday = byte.MaxValue;
    private const int YearScale = 10_000;
    private const int MonthScale = 100;

    public readonly record struct Rule(int Id, IScheduleRule Schedule, byte MinLevel, byte MaxLevel, byte CountdownMinutes);

    public readonly record struct Owner(EventScheduleKind Kind, int Id, string Name, byte Clock, byte Flags, IReadOnlyList<Rule> Rules,
        ushort DurationMinutes = 0);

    public static Packet Schedules(byte sub, string zoneId, int offsetMinutes, IReadOnlyList<Owner> owners)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteByte(sub);
        packet.WriteSByteString(zoneId);
        packet.WriteInt(offsetMinutes);
        packet.WriteUShort((ushort)owners.Count);
        foreach (var owner in owners)
        {
            packet.WriteByte((byte)owner.Kind);
            packet.WriteInt(owner.Id);
            packet.WriteSByteString(owner.Name);
            packet.WriteByte(owner.Clock);
            packet.WriteByte(owner.Flags);
            packet.WriteUShort(owner.DurationMinutes);
            packet.WriteUShort((ushort)owner.Rules.Count);
            foreach (var rule in owner.Rules)
            {
                packet.WriteInt(rule.Id);
                packet.WriteByte((byte)rule.Schedule.Repeat);
                packet.WriteInt(rule.Schedule.Date is { } date ? DateNumber(date) : 0);
                packet.WriteByte(rule.Schedule.Day is { } day ? (byte)day : NoWeekday);
                packet.WriteByte(rule.Schedule.DayOfMonth);
                packet.WriteByte(rule.Schedule.Hour);
                packet.WriteByte(rule.Schedule.Minute);
                packet.WriteByte(rule.MinLevel);
                packet.WriteByte(rule.MaxLevel);
                packet.WriteByte(rule.CountdownMinutes);
            }
        }

        return packet;
    }

    public static int DateNumber(DateOnly date) => date.Year * YearScale + date.Month * MonthScale + date.Day;

    public static DateOnly? DateFromNumber(int number)
    {
        if (number <= 0)
            return null;
        try
        {
            return new DateOnly(number / YearScale, number / MonthScale % MonthScale, number % MonthScale);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
