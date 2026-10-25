using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class EventCalendarTests
{
    private static readonly TimeZoneInfo PlusOne = TimeZoneInfo.CreateCustomTimeZone("plus-one", TimeSpan.FromHours(1), "plus-one", "plus-one");
    private static readonly TimeZoneInfo MinusThree = TimeZoneInfo.CreateCustomTimeZone("minus-three", TimeSpan.FromHours(-3), "minus-three", "minus-three");

    private static EventRule Rule(ScheduleRepeat repeat, int hour, DateOnly? date = null, DayOfWeek? day = null, int dayOfMonth = 0) =>
        new(1, repeat, date, day, dayOfMonth, hour, 0, 0, 0, 0);

    private static EventOwner Owner(EventRuleClock clock, params EventRule[] rules) =>
        new(EventKind.Krowaz, 0, "Krowaz", clock, true, false, false, rules);

    [Fact]
    public void TheGridStartsOnTheMondayOnOrBeforeTheFirst()
    {
        Assert.Equal(new DateOnly(2026, 9, 28), EventCalendar.GridStart(2026, 10));
        Assert.Equal(new DateOnly(2027, 2, 1), EventCalendar.GridStart(2027, 2));
    }

    [Fact]
    public void AScheduleClockStartIsShownInLocalTimeOnTheRightDay()
    {
        var owner = Owner(EventRuleClock.Schedule, Rule(ScheduleRepeat.Once, 1, new DateOnly(2026, 10, 23)));

        var starts = EventCalendar.Starts(new[] { owner }, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), PlusOne, 60, MinusThree);

        var start = Assert.Single(starts);
        Assert.Equal(new DateTime(2026, 10, 22, 21, 0, 0), start.Local);
    }

    [Fact]
    public void AUtcStartIgnoresTheScheduleZone()
    {
        var owner = Owner(EventRuleClock.Utc, Rule(ScheduleRepeat.Weekly, 12, day: DayOfWeek.Sunday));

        var starts = EventCalendar.Starts(new[] { owner }, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), PlusOne, 60, TimeZoneInfo.Utc);

        Assert.Equal(new[] { 4, 11, 18, 25 }, starts.Select(s => s.Local.Day));
        Assert.All(starts, s => Assert.Equal(12, s.Local.Hour));
    }

    [Fact]
    public void WithoutTheZoneTheServersOffsetIsUsed()
    {
        var owner = Owner(EventRuleClock.Schedule, Rule(ScheduleRepeat.Monthly, 10, dayOfMonth: 31));

        var starts = EventCalendar.Starts(new[] { owner }, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28), null, 120, TimeZoneInfo.Utc);

        Assert.Equal(new DateTime(2027, 2, 28, 8, 0, 0), Assert.Single(starts).Local);
    }

    [Fact]
    public void TheSchedulesReplyCarriesEachOwnersRulesAndLength()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new NoConnection(), session, new LoginState()), () => 0);
        AdminEventSchedules? shown = null;
        session.Admin.AdminEventSchedulesEvent += s => shown = s;

        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminReplySubOpcode.EventSchedules);
        p.WriteSByteString("Europe/Berlin");
        p.WriteInt(120);
        p.WriteUShort(1);
        p.WriteByte((byte)EventKind.Temple);
        p.WriteInt(1);
        p.WriteSByteString("Chaos");
        p.WriteByte((byte)EventRuleClock.Schedule);
        p.WriteByte(1 | 4);
        p.WriteUShort(45);
        p.WriteUShort(1);
        p.WriteInt(7);
        p.WriteByte((byte)ScheduleRepeat.Once);
        p.WriteInt(20261224);
        p.WriteByte(byte.MaxValue);
        p.WriteByte(0);
        p.WriteByte(19);
        p.WriteByte(30);
        p.WriteByte(30);
        p.WriteByte(70);
        p.WriteByte(5);
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.NotNull(shown);
        Assert.Equal("Europe/Berlin", shown.ZoneId);
        Assert.Equal(120, shown.OffsetMinutes);
        var owner = Assert.Single(shown.Owners);
        Assert.True(owner.Editable);
        Assert.True(owner.HasLevels);
        Assert.False(owner.FromSettings);
        Assert.Equal(45, owner.DurationMinutes);
        Assert.Equal(new EventRule(7, ScheduleRepeat.Once, new DateOnly(2026, 12, 24), null, 0, 19, 30, 30, 70, 5), Assert.Single(owner.Rules));
    }

    [Fact]
    public void SettingALengthSendsTheEventAndItsMinutes()
    {
        var sink = new Recorder();

        new AdminRequests(sink).EventDurationSave(EventKind.Bowl, 0, 90);

        var p = Assert.Single(sink.Sent);
        p.ResetOffset();
        Assert.Equal((byte)AdminRequestSubOpcode.EventDurationSave, p.ReadByte());
        Assert.Equal((byte)EventKind.Bowl, p.ReadByte());
        Assert.Equal(0, p.ReadInt());
        Assert.Equal(90, p.ReadUShort());
    }

    private sealed class NoConnection : IPacketSink
    {
        public bool Connected => false;
        public void Send(Packet packet) { }
    }

    private sealed class Recorder : IPacketSink
    {
        public readonly List<Packet> Sent = new();
        public bool Connected => true;
        public void Send(Packet packet) => Sent.Add(packet);
    }
}
