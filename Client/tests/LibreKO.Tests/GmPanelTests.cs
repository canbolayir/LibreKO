using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class GmPanelTests
{
    private const int Ruby = 389010000;
    private const int Topaz = 389020000;

    private sealed class Recorder : IPacketSink
    {
        public readonly List<Packet> Sent = new();
        public bool Connected => true;
        public void Send(Packet packet) => Sent.Add(packet);
    }

    private static (PacketDispatcher Dispatcher, GameSession Session) Readers()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new Recorder(), session, new LoginState()), () => 0);
        return (dispatcher, session);
    }

    private static Packet Reply(AdminReplySubOpcode sub, Action<Packet> body)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)sub);
        body(p);
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void ADemoAccountSeesItsOwnCharacterPagesAndTravel()
    {
        var demo = GmPanelPages.For(gameMaster: false);
        var gm = GmPanelPages.For(gameMaster: true);

        Assert.All(demo, page => Assert.False(page.GameMasterOnly));
        Assert.Equal(
            GmPanelPages.All.Where(page => page.Group == GmPanelPages.SelfGroup).Select(page => page.Id).Append(GmPanelPages.Travel).OrderBy(id => id),
            demo.Select(page => page.Id).OrderBy(id => id));
        Assert.Equal(GmPanelPages.All.Count, gm.Count);
        Assert.Equal(GmPanelPages.SelfCharacter, GmPanelPages.FirstPage(false));
        Assert.Equal(GmPanelPages.Find, GmPanelPages.FirstPage(true));
    }

    [Theory]
    [InlineData("5", 500)]
    [InlineData("5%", 500)]
    [InlineData("0,25", 25)]
    [InlineData(" 0.01 ", 1)]
    [InlineData("100", 10_000)]
    public void AChanceIsTypedAsAPercent(string text, int expected)
    {
        Assert.True(GmPanelText.TryParsePercent(text, 10_000, out int chance));
        Assert.Equal(expected, chance);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100.5")]
    [InlineData("abc")]
    [InlineData("")]
    public void AChanceOutsideOneHundredthToAHundredIsRefused(string text) =>
        Assert.False(GmPanelText.TryParsePercent(text, 10_000, out _));

    [Fact]
    public void ChancesAreShownAsPercentWithUpToTwoDecimals()
    {
        Assert.Equal("5", GmPanelText.Percent(500, 10_000));
        Assert.Equal("0.25", GmPanelText.Percent(25, 10_000));
        Assert.Equal("12.5", GmPanelText.Percent(1250, 10_000));
    }

    [Fact]
    public void LastSeenReadsInMinutesHoursAndDays()
    {
        var now = new DateTime(2026, 10, 23, 20, 0, 0, DateTimeKind.Utc);

        Assert.Equal("Seen just now", GmPanelText.SeenAgo(now.AddSeconds(-20), now));
        Assert.Equal("Seen 12 min ago", GmPanelText.SeenAgo(now.AddMinutes(-12), now));
        Assert.Equal("Seen 5 h ago", GmPanelText.SeenAgo(now.AddHours(-5), now));
        Assert.Equal("Seen yesterday", GmPanelText.SeenAgo(now.AddDays(-1.2), now));
        Assert.Equal("Seen 12 days ago", GmPanelText.SeenAgo(now.AddDays(-12), now));
        Assert.Equal("Offline", GmPanelText.SeenAgo(null, now));
    }

    [Fact]
    public void TheCalendarStepsAndSpansByMonthWeekAndDay()
    {
        var thursday = new DateOnly(2026, 10, 22);

        Assert.Equal(new DateOnly(2026, 10, 19), EventCalendar.WeekStart(thursday));
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), EventCalendar.Range(CalendarView.Week, thursday));
        Assert.Equal((thursday, thursday), EventCalendar.Range(CalendarView.Day, thursday));
        Assert.Equal(EventCalendar.GridStart(2026, 10), EventCalendar.Range(CalendarView.Month, thursday).First);
        Assert.Equal(new DateOnly(2026, 10, 29), EventCalendar.Step(CalendarView.Week, thursday, 1));
        Assert.Equal(new DateOnly(2026, 10, 21), EventCalendar.Step(CalendarView.Day, thursday, -1));
        Assert.Equal(new DateOnly(2026, 11, 1), EventCalendar.Step(CalendarView.Month, thursday, 1));
        Assert.Equal("October 2026", EventCalendar.Title(CalendarView.Month, thursday));
        Assert.Equal("19 - 25 October 2026", EventCalendar.Title(CalendarView.Week, thursday));
    }

    [Fact]
    public void APlayerFindRowCarriesOnlineClassNationAndLastSeen()
    {
        var (dispatcher, session) = Readers();
        List<AdminFindHit>? shown = null;
        session.Admin.AdminFindEvent += (_, _, hits) => shown = hits;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.Find, p =>
        {
            p.WriteByte((byte)AdminFindKind.Players);
            p.WriteUShort(2);
            p.WriteUShort(2);
            foreach (var (id, name, online, seen) in new[] { (2, "Rikka", true, 0u), (7, "Rias", false, 1_792_000_000u) })
            {
                p.WriteInt(id);
                p.WriteInt(0);
                p.WriteSByteString(name);
                p.WriteShort(70);
                p.WriteByte(21);
                p.WriteUShort(812);
                p.WriteUShort(433);
                p.WriteByte((byte)(online ? 4 : 0));
                p.WriteShort(211);
                p.WriteByte(2);
                p.WriteUInt(seen);
            }
        }));

        Assert.NotNull(shown);
        Assert.True(shown[0].Online);
        Assert.Equal(211, shown[0].Class);
        Assert.Equal(2, shown[0].Nation);
        Assert.Null(shown[0].LastSeen);
        Assert.False(shown[1].Online);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_792_000_000).UtcDateTime, shown[1].LastSeen);
    }

    [Fact]
    public void ThePlayerSheetCarriesTheGearAndBag()
    {
        var (dispatcher, session) = Readers();
        AdminPlayerSheet? shown = null;
        session.Admin.AdminPlayerSheetEvent += sheet => shown = sheet;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.PlayerSheet, p =>
        {
            p.WriteInt(2);
            p.WriteSByteString("Rikka");
            p.WriteByte(1);
            p.WriteByte(72);
            p.WriteShort(211);
            p.WriteByte(2);
            p.WriteByte(21);
            p.WriteUShort(812);
            p.WriteUShort(433);
            p.WriteInt(18_420_000);
            p.WriteInt(1_250);
            p.WriteInt(30_115);
            p.WriteUInt(0);
            p.WriteByte(2);
            p.WriteByte(InventoryConstants.Head);
            p.WriteInt(Ruby);
            p.WriteUShort(1);
            p.WriteShort(9000);
            p.WriteByte(0);
            p.WriteByte(InventoryConstants.InventoryStart + 1);
            p.WriteInt(Topaz);
            p.WriteUShort(48);
            p.WriteShort(1);
            p.WriteByte(0);
        }));

        Assert.NotNull(shown);
        Assert.Equal("Rikka", shown.Name);
        Assert.True(shown.Online);
        Assert.Equal(18_420_000, shown.Gold);
        Assert.Equal(1_250, shown.KnightCash);
        Assert.Equal(Ruby, shown.At(InventoryConstants.Head)?.ItemId);
        Assert.Equal(48, shown.At(InventoryConstants.InventoryStart + 1)?.Count);
        Assert.Null(shown.At(InventoryConstants.InventoryStart));
    }

    [Fact]
    public void TheGroupListCarriesEachGroupsSizeAndFirstItems()
    {
        var (dispatcher, session) = Readers();
        AdminItemGroups? shown = null;
        session.Admin.AdminItemGroupsEvent += groups => shown = groups;

        dispatcher.Dispatch(Reply(AdminReplySubOpcode.ItemGroups, p =>
        {
            p.WriteUShort(41);
            p.WriteByte(1);
            p.WriteInt(9001);
            p.WriteUShort(12);
            p.WriteByte(2);
            p.WriteInt(Ruby);
            p.WriteInt(Topaz);
        }));

        Assert.NotNull(shown);
        Assert.Equal(41, shown.Total);
        var row = Assert.Single(shown.Rows);
        Assert.Equal(9001, row.Group);
        Assert.Equal(12, row.ItemCount);
        Assert.Equal(new[] { Ruby, Topaz }, row.Samples);
    }

    [Fact]
    public void BagEditsAndListsAreSentWithTheirFields()
    {
        var sink = new Recorder();
        var requests = new AdminRequests(sink);

        requests.InventoryEdit(2, AdminInventoryEdit.Put, InventoryConstants.InventoryStart + 3, Ruby, 50);
        requests.PlayerSheetRequest(7);
        requests.ItemGroupsRequest("ruby");

        var edit = sink.Sent[0];
        edit.ResetOffset();
        Assert.Equal((byte)AdminRequestSubOpcode.InventoryEdit, edit.ReadByte());
        Assert.Equal(2, edit.ReadInt());
        Assert.Equal((byte)AdminInventoryEdit.Put, edit.ReadByte());
        Assert.Equal(InventoryConstants.InventoryStart + 3, edit.ReadByte());
        Assert.Equal(Ruby, edit.ReadInt());
        Assert.Equal(50, edit.ReadUShort());
        var sheet = sink.Sent[1];
        sheet.ResetOffset();
        Assert.Equal((byte)AdminRequestSubOpcode.PlayerSheet, sheet.ReadByte());
        Assert.Equal(7, sheet.ReadInt());
        var groups = sink.Sent[2];
        groups.ResetOffset();
        Assert.Equal((byte)AdminRequestSubOpcode.ItemGroups, groups.ReadByte());
        Assert.Equal("ruby", groups.ReadUtf8String());
    }

    [Fact]
    public void MonstersAreFoundByEveryWordOfTheirNameOrByTheirId()
    {
        NpcNames.AddMonster(910750, "Giant Worm");
        NpcNames.AddMonster(910751, "Worm");
        NpcNames.AddMonster(910752, "Worm Giant Queen");

        var byWords = NpcNames.Search("giant worm", monsters: true, 10).Select(hit => hit.Id).ToList();
        var byId = NpcNames.Search("910751", monsters: true, 10);

        Assert.Contains(910750, byWords);
        Assert.Contains(910752, byWords);
        Assert.DoesNotContain(910751, byWords);
        Assert.Equal(910751, byId[0].Id);
    }

    [Fact]
    public void PresenceAndFoundCountReadTheSameEverywhere()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("Online", GmPanelText.Presence(true, now.AddDays(-3), now));
        Assert.Equal("Seen 3 days ago", GmPanelText.Presence(false, now.AddDays(-3), now));
        Assert.Equal("first 50 of 120", GmPanelText.FoundCount(50, 120));
        Assert.Equal("7 found", GmPanelText.FoundCount(7, 7));
    }

    [Theory]
    [InlineData("1,000,000", 1_000_000)]
    [InlineData(" 250 ", 250)]
    [InlineData("-40", -40)]
    public void AmountsAcceptThousandSeparators(string text, long expected)
    {
        Assert.True(GmPanelText.TryParseAmount(text, out long amount));
        Assert.Equal(expected, amount);
        Assert.False(GmPanelText.TryParseAmount("ten", out _));
    }

    [Fact]
    public void TheCalendarNamesItsDaysAndSummarisesStarts()
    {
        Assert.Equal(DayOfWeek.Monday, EventCalendar.WeekOrder[0]);
        Assert.Equal(DayOfWeek.Sunday, EventCalendar.WeekOrder[^1]);
        Assert.Equal("Wed", EventCalendar.ShortName(DayOfWeek.Wednesday));
        Assert.Equal("Every week", EventCalendar.RepeatName(ScheduleRepeat.Weekly));

        var rules = Enumerable.Range(0, 4)
            .Select(hour => new EventRule(hour + 1, ScheduleRepeat.Daily, null, null, 0, hour, 0, 0, 0, 0))
            .ToList();
        Assert.Equal("No starts", EventCalendar.Summary([], 3));
        Assert.Equal("Every day 00:00 · Every day 01:00 · Every day 02:00 · +1", EventCalendar.Summary(rules, 3));
    }

    [Fact]
    public void EventCommandsComeFromOneTable()
    {
        EventOwner Owner(EventKind kind, int id) => new(kind, id, "x", EventRuleClock.Schedule, true, false, false, []);

        Assert.Equal("+utc open", GmCommands.StartEvent(Owner(EventKind.Temple, (int)TempleEventType.UnderTheCastle)));
        Assert.Equal("+manes cancel", GmCommands.StopEvent(Owner(EventKind.Temple, (int)TempleEventType.ManesSurvival)));
        Assert.Equal("+templecancel", GmCommands.StopEvent(Owner(EventKind.Temple, (int)TempleEventType.Chaos)));
        Assert.Equal("+lottery start 7", GmCommands.StartEvent(Owner(EventKind.Lottery, 7)));
        Assert.Null(GmCommands.StartEvent(Owner(EventKind.NationWar, 1)));
        Assert.Equal("+goto Zeus", GmCommands.ForPlayer(GmCommands.Goto, "Zeus"));
    }

    [Fact]
    public void SharedRulesAnswerForTheGmPages()
    {
        Assert.Equal("Moradon", ZoneCatalog.Name(21));
        Assert.Null(ZoneCatalog.Find(250));
        Assert.Equal("Zone 250", ZoneCatalog.Name(250));
        Assert.True(InventoryConstants.IsGridSlot(InventoryConstants.InventoryStart));
        Assert.False(InventoryConstants.IsGridSlot(InventoryConstants.CospreStart));
        Assert.False(InventoryConstants.IsGridSlot(InventoryConstants.Head));
        Assert.True(AdminDropSlot.IsGroup(AdminDropSlot.FirstGroupId));
        Assert.False(AdminDropSlot.IsGroup(Ruby));
        Assert.False(AdminDropSlot.IsGroup(0));
    }
}
