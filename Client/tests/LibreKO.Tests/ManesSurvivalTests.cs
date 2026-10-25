using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class ManesSurvivalTests
{
    [Theory]
    [InlineData(57, "shoppingmall", true)]
    [InlineData(60, "mail", true)]
    [InlineData(58, "globalmap", true)]
    [InlineData(59, "merchantmenu", true)]
    [InlineData(57, "pet", false)]
    [InlineData(21, "shoppingmall", false)]
    public void TheTaskbarWindowsTwentySixNineteenRefusesStayShutInTheArenas(int zone, string window, bool refused) =>
        Assert.Equal(refused, ManesSurvival.RefusesWindow(zone, window));

    private const int Me = 4100;
    private const int Rival = 4200;
    private const int Smash1 = 301;
    private const int Smash2 = 302;
    private const int Smash3 = 303;
    private const int SmallHpPotion = 6201;
    private const int Bread = 389010000;

    private const string Table = """
        {
         "looks": [
          {"row": 1, "class": 101, "race": 1, "right": 1110531575, "left": 0, "armour": [220001005, 220002005, 0, 220004005, 220005005, 220003005, 0, 0, 0, 0]},
          {"row": 3, "class": 201, "race": 11, "right": 1110531575, "left": 0, "armour": [220001005, 220002005, 0, 220004005, 220005005, 220003005, 0, 0, 0, 0]}
         ],
         "picks": [
          {"code": 301, "kind": 1, "magic": 491202, "item": 0, "count": 0, "price": 0, "hp": 0, "attackUp": 0, "attackDown": 0},
          {"code": 5901, "kind": 5, "magic": 491330, "item": 0, "count": 0, "price": 0, "hp": 40, "attackUp": 3, "attackDown": 2},
          {"code": 5902, "kind": 5, "magic": 491331, "item": 0, "count": 0, "price": 0, "hp": 0, "attackUp": 0, "attackDown": 5},
          {"code": 6201, "kind": 100, "magic": 491410, "item": 978023000, "count": 10, "price": 3000, "hp": 0, "attackUp": 0, "attackDown": 0}
         ]
        }
        """;

    private sealed class RecordingSink : IPacketSink
    {
        public List<Packet> Sent { get; } = [];

        public bool Connected => true;

        public void Send(Packet packet) => Sent.Add(packet);
    }

    private static (PacketDispatcher Dispatcher, GameSession Session) Readers()
    {
        var session = new GameSession();
        session.Self.ApplyCharacterId(Me);
        session.Self.Inventory.EnsureLength(Inventory.GridStart + Inventory.GridCount);
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new RecordingSink(), session, new LoginState()), () => 0);
        return (dispatcher, session);
    }

    private static Packet Survival(ManesSurvivalSubOpcode sub, byte kind, Action<Packet> body)
    {
        var p = new Packet(GameOpcodes.GS_MANES_SURVIVAL);
        p.WriteByte((byte)sub);
        p.WriteByte(kind);
        body(p);
        p.ResetOffset();
        return p;
    }

    private static Packet Sent(Action<IManesSurvivalRequests> send)
    {
        var sink = new RecordingSink();
        send(new ManesSurvivalRequests(sink, new ItemsSession()));
        var packet = Assert.Single(sink.Sent);
        Assert.Equal((byte)GameOpcodes.GS_MANES_SURVIVAL, packet.GetOpcode());
        packet.ResetOffset();
        return packet;
    }

    [Fact]
    public void TheTableReadsLooksAndPicks()
    {
        var book = ManesSurvival.Parse(Table);
        Assert.Equal(201, book.Look(3)!.Class);
        Assert.Equal(978023000, book.Pick(SmallHpPotion)!.Item);
        Assert.Equal(5901, book.PickOfMagic(491330)!.Code);
        Assert.Null(book.Pick(9999));
    }

    [Theory]
    [InlineData(Smash1, Smash2, ManesFit.Replaces)]
    [InlineData(Smash3, Smash2, ManesFit.AlreadyHeld)]
    [InlineData(Smash2, Smash2, ManesFit.AlreadyHeld)]
    [InlineData(401, Smash2, ManesFit.Free)]
    public void AHeldSkillOfTheSameGroupIsReplacedOnlyByAHigherOne(int held, int offered, ManesFit fit) =>
        Assert.Equal(fit, ManesSurvival.Fit(held, offered));

    [Fact]
    public void TheTimersRunFromTheEventSeconds()
    {
        Assert.Equal(180, ManesSurvival.NonBattleLeft(ManesSurvival.EventSeconds));
        Assert.Equal(600, ManesSurvival.InvincibleLeft(ManesSurvival.EventSeconds));
        Assert.Equal(0, ManesSurvival.NonBattleLeft(900));
        Assert.Equal("20:00", ManesSurvival.Clock(ManesSurvival.EventSeconds));
    }

    [Fact]
    public void KarusRowsWearTheKarusLook()
    {
        Assert.Equal(ManesSurvival.KarusLook, ManesSurvival.LookSkill(2));
        Assert.Equal(ManesSurvival.ElMoradLook, ManesSurvival.LookSkill(3));
        Assert.True(ManesSurvival.IsZone(57));
        Assert.True(ManesSurvival.IsZone(60));
        Assert.False(ManesSurvival.IsZone(61));
    }

    [Fact]
    public void SignUpRefusalsNameTheirText()
    {
        Assert.Equal(ManesSurvival.DuplicateIpText, ManesSurvival.SignUpText(-4));
        Assert.Equal(ManesSurvival.LevelText, ManesSurvival.SignUpText(-1));
        Assert.Equal(0, ManesSurvival.SignUpText(1));
    }

    [Fact]
    public void PickFailuresCarryTheirCode()
    {
        Assert.Equal("broke", ManesSurvival.PickFailure(-11, (id, _) => id == ManesSurvival.NotEnoughNoahText ? "broke" : ""));
        Assert.Equal("Failed to acquire skill. (10)", ManesSurvival.PickFailure(-10, (_, fallback) => fallback));
    }

    [Fact]
    public void PassivesSumWithHpTimesTen()
    {
        var book = ManesSurvival.Parse(Table);
        var totals = ManesSurvival.Totals([book.Pick(5901)!, book.Pick(5902)!]);
        Assert.Equal(new ManesPassiveTotals(400, 3, 7), totals);
    }

    [Fact]
    public void TheSignUpBoardOpensWithItsClockAndCount()
    {
        var (dispatcher, session) = Readers();
        ManesSignUp? seen = null;
        session.ManesSurvival.SignUpEvent += s => seen = s;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.SignUp, (byte)ManesSignUpKind.Open, p =>
        {
            p.WriteUShort(300);
            p.WriteShort(12);
        }));

        Assert.Equal(new ManesSignUp(ManesSignUpKind.Open, 300, 12, 0), seen);
    }

    [Fact]
    public void AnAcceptedEntryCarriesTheCountAndARefusalOnlyItsCode()
    {
        var (dispatcher, session) = Readers();
        var seen = new List<ManesSignUp>();
        session.ManesSurvival.SignUpEvent += seen.Add;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.SignUp, (byte)ManesSignUpKind.Entry, p =>
        {
            p.WriteShort(ManesSurvivalWire.SignUpApplied);
            p.WriteUShort(13);
        }));
        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.SignUp, (byte)ManesSignUpKind.Entry, p => p.WriteShort(-3)));

        Assert.Equal(new ManesSignUp(ManesSignUpKind.Entry, 0, 13, ManesSurvivalWire.SignUpApplied), seen[0]);
        Assert.Equal(new ManesSignUp(ManesSignUpKind.Entry, 0, 0, -3), seen[1]);
    }

    [Fact]
    public void TheStartSetsTheEventLevelAndEmptiesTheBag()
    {
        var (dispatcher, session) = Readers();
        session.Self.SetInventorySlot(Inventory.GridStart + 3, new ItemSlot { ItemId = Bread, Count = 5 });
        ManesStart? seen = null;
        session.ManesSurvival.StartEvent += s => seen = s;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Start, p =>
        {
            p.WriteByte(3);
            p.WriteShort(1200);
            p.WriteShort(500);
            p.WriteShort(20);
            p.WriteByte(1);
        }));

        Assert.Equal(new ManesStart(3, 1200, 500, 20, 1), seen);
        Assert.True(session.ManesSurvival.Running);
        Assert.Equal(3, session.ManesSurvival.Row);
        Assert.Equal(1, session.Self.Sheet.Level);
        Assert.Equal(500, session.Self.Sheet.MaxExp);
        Assert.True(session.Self.Inventory[Inventory.GridStart + 3].IsEmpty);
    }

    [Fact]
    public void TheBossBarCarriesNameHpAndSeconds()
    {
        var (dispatcher, session) = Readers();
        ManesBoss? seen = null;
        session.ManesSurvival.BossEvent += b => seen = b;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Boss, p =>
        {
            p.WriteString("Manes");
            p.WriteUInt(900000);
            p.WriteUInt(450000);
            p.WriteUInt(840);
        }));

        Assert.Equal(new ManesBoss("Manes", 900000, 450000, 840), seen);
    }

    [Fact]
    public void TheRankBoardReadsMyRowAndEachRanker()
    {
        var (dispatcher, session) = Readers();
        ManesRank? seen = null;
        session.ManesSurvival.RankEvent += r => seen = r;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Rank, p =>
        {
            p.WriteByte(2);
            p.WriteShort(7);
            p.WriteInt(3400);
            p.WriteByte(2);
            p.WriteByte(Nations.Karus);
            p.WriteShort(-1);
            p.WriteString("Wolves");
            p.WriteString("Ares");
            p.WriteShort(9);
            p.WriteInt(5100);
            p.WriteByte(Nations.ElMorad);
            p.WriteShort(-1);
            p.WriteString("");
            p.WriteString("Zeus");
            p.WriteShort(7);
            p.WriteInt(3400);
        }));

        Assert.NotNull(seen);
        Assert.Equal(2, seen.MyRank);
        Assert.Equal(3400, seen.MyScore);
        Assert.Equal(2, seen.Rows.Count);
        Assert.Equal(new ManesRankRow(Nations.Karus, -1, "Wolves", "Ares", 9, 5100), seen.Rows[0]);
        Assert.Equal("Zeus", seen.Rows[1].Name);
    }

    [Fact]
    public void TheEndRestoresTheRealLevelAndBag()
    {
        var (dispatcher, session) = Readers();
        ManesEnd? seen = null;
        session.ManesSurvival.EndEvent += e => seen = e;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.End, p =>
        {
            p.WriteLong(9_000_000);
            p.WriteLong(1_234_567);
            p.WriteByte(70);
            for (int i = 0; i < ManesSurvival.BagSlots; i++)
            {
                p.WriteInt(i == 4 ? Bread : 0);
                p.WriteShort(0);
                p.WriteShort((short)(i == 4 ? 8 : 0));
                p.WriteByte(0);
                p.WriteShort(0);
                p.WriteInt(0);
                p.WriteInt(0);
            }
        }));

        Assert.NotNull(seen);
        Assert.False(session.ManesSurvival.Running);
        Assert.Equal(70, session.Self.Sheet.Level);
        Assert.Equal(1_234_567, session.Self.Sheet.Exp);
        Assert.Equal(ManesSurvival.BagSlots, seen.Bag.Length);
        Assert.Equal(Bread, session.Self.Inventory[Inventory.GridStart + 4].ItemId);
        Assert.Equal(8, session.Self.Inventory[Inventory.GridStart + 4].Count);
    }

    [Fact]
    public void KillAndRewardLinesReadEveryField()
    {
        var (dispatcher, session) = Readers();
        ManesKill? kill = null;
        ManesReward? reward = null;
        session.ManesSurvival.KillEvent += k => kill = k;
        session.ManesSurvival.RewardEvent += r => reward = r;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Kill, p =>
        {
            p.WriteByte(Nations.Karus);
            p.WriteString("Ares");
            p.WriteByte(Nations.ElMorad);
            p.WriteString("Zeus");
            p.WriteUInt(150);
        }));
        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Reward, p =>
        {
            p.WriteByte(1);
            p.WriteUInt(5100);
            p.WriteInt(Bread);
            p.WriteByte(3);
            p.WriteByte(0);
        }));

        Assert.Equal(new ManesKill(Nations.Karus, "Ares", Nations.ElMorad, "Zeus", 150), kill);
        Assert.Equal(new ManesReward(1, 5100, Bread, 3, false), reward);
    }

    [Fact]
    public void MarkersArriveAsPoints()
    {
        var (dispatcher, session) = Readers();
        IReadOnlyList<ManesMarker>? seen = null;
        session.ManesSurvival.MarkersEvent += m => seen = m;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Notice, (byte)ManesNoticeKind.Markers, p =>
        {
            p.WriteByte(2);
            p.WriteShort(100);
            p.WriteShort(200);
            p.WriteShort(300);
            p.WriteShort(400);
        }));

        Assert.Equal([new ManesMarker(100, 200), new ManesMarker(300, 400)], seen);
    }

    [Fact]
    public void AnOwnLevelUpSetsTheSheetAndAnotherOnlyAnnouncesIt()
    {
        var (dispatcher, session) = Readers();
        int peer = 0;
        session.Progress.PeerLevelChangeEvent += (id, _) => peer = id;

        void LevelUp(int id) => dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Progress, (byte)ManesProgressKind.Level, p =>
        {
            p.WriteInt(id);
            p.WriteByte(4);
            p.WriteShort(900);
            p.WriteShort(10);
            p.WriteShort(3000);
            p.WriteShort(2800);
            p.WriteShort(1500);
            p.WriteShort(1400);
        }));
        LevelUp(Me);
        LevelUp(Rival);

        Assert.Equal(4, session.Self.Sheet.Level);
        Assert.Equal(900, session.Self.Sheet.MaxExp);
        Assert.Equal(Rival, peer);
    }

    [Fact]
    public void TheExpAndScoreUpdatesAreApplied()
    {
        var (dispatcher, session) = Readers();

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Progress, (byte)ManesProgressKind.Exp, p =>
        {
            p.WriteByte(0);
            p.WriteShort(240);
        }));
        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Progress, (byte)ManesProgressKind.Score, p => p.WriteInt(1700)));

        Assert.Equal(240, session.Self.Sheet.Exp);
        Assert.Equal(1700, session.ManesSurvival.Score);
    }

    [Fact]
    public void AnItemPutInTheBagFillsItsSlotOrItsCount()
    {
        var (dispatcher, session) = Readers();

        void Put(short count) => dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Put, p =>
        {
            p.WriteByte(2);
            p.WriteInt(Bread);
            p.WriteShort(count);
            p.WriteShort(0);
            p.WriteByte(0);
        }));
        Put(1);
        Put(6);

        Assert.Equal(Bread, session.Self.Inventory[Inventory.GridStart + 2].ItemId);
        Assert.Equal(6, session.Self.Inventory[Inventory.GridStart + 2].Count);
    }

    [Fact]
    public void OffersCarrySkillsAndPotionsAndABonusOnlySkills()
    {
        var (dispatcher, session) = Readers();
        var seen = new List<ManesOffer>();
        session.ManesSurvival.OfferEvent += seen.Add;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Offer, p =>
        {
            p.WriteByte(2);
            p.WriteShort(Smash1);
            p.WriteShort(401);
            p.WriteByte(1);
            p.WriteShort(SmallHpPotion);
        }));
        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.BonusOffer, p =>
        {
            p.WriteByte(1);
            p.WriteShort(Smash2);
        }));

        Assert.Equal([Smash1, 401], seen[0].Skills);
        Assert.Equal([SmallHpPotion], seen[0].Potions);
        Assert.False(seen[0].Bonus);
        Assert.Equal([Smash2], seen[1].Skills);
        Assert.Empty(seen[1].Potions);
        Assert.True(seen[1].Bonus);
    }

    [Fact]
    public void PickRepliesCarryTheirActionAndResult()
    {
        var (dispatcher, session) = Readers();
        var seen = new List<ManesPickReply>();
        session.ManesSurvival.PickReplyEvent += seen.Add;
        int passive = 0;
        session.ManesSurvival.PassiveEvent += code => passive = code;

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Choice, p =>
        {
            p.WriteByte((byte)ManesPickAction.Potion);
            p.WriteShort(-11);
        }));
        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.BonusChoice, p => p.WriteShort(ManesSurvivalWire.PickSucceeded)));
        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Passive, p => p.WriteShort(5901)));

        Assert.Equal(new ManesPickReply(ManesPickAction.Potion, -11, false), seen[0]);
        Assert.Equal(new ManesPickReply(ManesPickAction.Skill, ManesSurvivalWire.PickSucceeded, true), seen[1]);
        Assert.Equal(5901, passive);
    }

    [Fact]
    public void OffersQueueAndAnEmptyOneIsIgnored()
    {
        var (_, session) = Readers();
        var manes = session.ManesSurvival;

        manes.ApplyOffer(new ManesOffer([], [SmallHpPotion], false));
        manes.ApplyOffer(new ManesOffer([Smash1], [SmallHpPotion], false));
        manes.ApplyOffer(new ManesOffer([Smash2], [], true));

        Assert.Equal(2, manes.Offers.Count);
        manes.TakeOffer();
        Assert.True(manes.Offers[0].Bonus);
    }

    [Fact]
    public void TheStartAndTheEndClearTheEventState()
    {
        var (_, session) = Readers();
        var manes = session.ManesSurvival;
        manes.KeepHotbar([ManesSurvival.StrokeSkill]);
        manes.KeepPassive(5901);
        manes.ApplyOffer(new ManesOffer([Smash1], [], false));

        manes.ApplyStart(new ManesStart(1, ManesSurvival.EventSeconds, 500, 0, 1));
        Assert.Null(manes.Hotbar);
        Assert.Empty(manes.Passives);
        Assert.Empty(manes.Offers);

        manes.KeepPassive(5901);
        manes.ApplyEnd(new ManesEnd(9000, 10, 70, []));
        Assert.False(manes.Running);
        Assert.Empty(manes.Passives);
    }

    [Fact]
    public void OtherPlayersAreHuntersUnlessTheViewerIsAGmOrHoldsTheSightBuff()
    {
        const int eyes = ManesSurvival.EyesSkill;
        var buffs = new Dictionary<int, double> { [eyes] = 100.0 };
        int TypeOf(int skill) => skill == eyes ? ManesSurvival.SightBuffType : 0;

        Assert.True(ManesSurvival.HasSight(buffs, 50.0, TypeOf));
        Assert.False(ManesSurvival.HasSight(buffs, 150.0, TypeOf));
        Assert.True(ManesSurvival.HidesNames(57, viewerIsGm: false, sight: false));
        Assert.False(ManesSurvival.HidesNames(57, viewerIsGm: true, sight: false));
        Assert.False(ManesSurvival.HidesNames(57, viewerIsGm: false, sight: true));
        Assert.False(ManesSurvival.HidesNames(21, viewerIsGm: false, sight: false));
    }

    [Theory]
    [InlineData(57, true)]
    [InlineData(37, true)]
    [InlineData(21, false)]
    public void TheSkillWindowStaysShutInTheEventZones(int zone, bool refused) =>
        Assert.Equal(refused, ZoneSkillTrees.SkillWindowRefused(zone));

    [Fact]
    public void ADiscardWaitsForItsReplyAndThenEmptiesTheSlot()
    {
        var (dispatcher, session) = Readers();
        int abs = Inventory.GridStart + 5;
        session.Self.SetInventorySlot(abs, new ItemSlot { ItemId = Bread, Count = 2 });
        var sink = new RecordingSink();
        new ManesSurvivalRequests(sink, session.Items).Discard(5, Bread);
        Assert.Equal(abs, session.Items.PendingRemoveSlot);

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Discard, p => p.WriteByte(1)));

        Assert.True(session.Self.Inventory[abs].IsEmpty);
    }

    [Fact]
    public void AnEventLootReplyTakesTheItemFromTheOpenBundle()
    {
        var (dispatcher, session) = Readers();
        (int Bundle, int Slot, int Item)? taken = null;
        session.Loot.LootTakenEvent += (bundle, slot, item) => taken = (bundle, slot, item);

        dispatcher.Dispatch(Survival(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Loot, p =>
        {
            p.WriteInt(77);
            p.WriteShort(4);
            p.WriteInt(Bread);
            p.WriteShort(1);
        }));

        Assert.Equal((77, ManesSurvivalWire.UnknownBundleSlot, Bread), taken);
    }

    [Fact]
    public void TheSignUpRequestCarriesItsAction()
    {
        var p = Sent(r => r.SignUp(apply: false));
        Assert.Equal((byte)ManesSurvivalSubOpcode.SignUp, p.ReadByte());
        Assert.Equal((byte)ManesSignUpKind.Entry, p.ReadByte());
        Assert.Equal((ushort)ManesSignUpAction.Cancel, p.ReadUShort());
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void TheRankRequestIsThreeBytes()
    {
        var p = Sent(r => r.Rank());
        Assert.Equal((byte)ManesSurvivalSubOpcode.State, p.ReadByte());
        Assert.Equal((byte)ManesStateKind.Rank, p.ReadByte());
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void PickRequestsNameTheirActionAndCode()
    {
        var pick = Sent(r => r.Pick(Smash1));
        Assert.Equal((byte)ManesSurvivalSubOpcode.Pick, pick.ReadByte());
        Assert.Equal((byte)ManesPickKind.Choice, pick.ReadByte());
        Assert.Equal((byte)ManesPickAction.Skill, pick.ReadByte());
        Assert.Equal(Smash1, pick.ReadShort());

        var potion = Sent(r => r.BuyPotion(SmallHpPotion));
        potion.ReadByte();
        potion.ReadByte();
        Assert.Equal((byte)ManesPickAction.Potion, potion.ReadByte());
        Assert.Equal(SmallHpPotion, potion.ReadShort());

        var skip = Sent(r => r.SkipPick());
        skip.ReadByte();
        skip.ReadByte();
        Assert.Equal((byte)ManesPickAction.Skip, skip.ReadByte());
        Assert.Equal(0, skip.RemainingBytes);

        var bonus = Sent(r => r.BonusPick(Smash2));
        bonus.ReadByte();
        Assert.Equal((byte)ManesPickKind.BonusChoice, bonus.ReadByte());
        Assert.Equal((byte)ManesPickAction.Skill, bonus.ReadByte());
        Assert.Equal(Smash2, bonus.ReadShort());
    }

    [Fact]
    public void BagRequestsMatchTheEventZoneLayouts()
    {
        var loot = Sent(r => r.Loot(77, Bread, 2));
        Assert.Equal((byte)ManesSurvivalSubOpcode.Bag, loot.ReadByte());
        Assert.Equal((byte)ManesBagKind.Loot, loot.ReadByte());
        Assert.Equal(77, loot.ReadInt());
        Assert.Equal(Bread, loot.ReadInt());
        Assert.Equal((ushort)2, loot.ReadUShort());
        Assert.Equal(0, loot.RemainingBytes);

        var discard = Sent(r => r.Discard(5, Bread));
        discard.ReadByte();
        Assert.Equal((byte)ManesBagKind.Discard, discard.ReadByte());
        Assert.Equal((byte)5, discard.ReadByte());
        Assert.Equal(Bread, discard.ReadInt());
        Assert.Equal(0, discard.RemainingBytes);
    }
}
