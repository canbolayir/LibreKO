using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class ManesSurvivalTests : GameTestBase
{
    private const int BagItem = 389010000;
    private const int FirstPrize = 379156000;
    private const int HpPotion = 978023000;
    private const int BossKillerItem = 900184000;
    private const string BossName = "Shaitan";
    private const byte RealLevel = 60;
    private const long RealExperience = 1234;

    [Theory]
    [InlineData(AccountNation.Karus, 101, ManesSurvivalRules.KarusWarriorRow)]
    [InlineData(AccountNation.Karus, 102, ManesSurvivalRules.KarusWarriorRow)]
    [InlineData(AccountNation.Karus, 103, ManesSurvivalRules.KarusPriestRow)]
    [InlineData(AccountNation.Karus, 104, ManesSurvivalRules.KarusPriestRow)]
    [InlineData(AccountNation.ElMorad, 201, ManesSurvivalRules.ElMoradWarriorRow)]
    [InlineData(AccountNation.ElMorad, 211, ManesSurvivalRules.ElMoradPriestRow)]
    [InlineData(AccountNation.ElMorad, 213, ManesSurvivalRules.ElMoradWarriorRow)]
    public void ThePriestRowIsForPriestsAndMages(AccountNation nation, short classId, byte row) =>
        ManesSurvivalRules.RowFor(nation, classId).Should().Be(row);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(50, 1)]
    [InlineData(51, 2)]
    [InlineData(500, ManesSurvivalRules.ArenaCount)]
    public void ArenasOpenAsTheSignUpsFillThem(int players, int arenas) =>
        ManesSurvivalRules.ArenasFor(players, 50).Should().Be(arenas);

    [Fact]
    public void ZonesFiftySevenToSixtyAreTheArenasAndSendAReconnectHome()
    {
        for (byte zone = ManesSurvivalRules.FirstZone; zone <= ManesSurvivalRules.LastZone; zone++)
        {
            ManesSurvivalRules.IsZone(zone).Should().BeTrue();
            CharacterReconnectZoneRepair.IsEventZone(zone).Should().BeTrue();
            ZoneRules.AllowsPets(zone).Should().BeFalse();
        }
        ManesSurvivalRules.IsZone(ManesSurvivalRules.FirstZone - 1).Should().BeFalse();
        ManesSurvivalRules.IsZone(ManesSurvivalRules.LastZone + 1).Should().BeFalse();
    }

    [Fact]
    public async Task TheStartTakesTheBagAndEverySaveKeepsTheRealCharacter()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var player = Player(sessions, 500, AccountNation.Karus, sent);

        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);

        player.ZoneId.Should().Be(ManesSurvivalRules.FirstZone);
        player.Level.Should().Be(ManesSurvivalRules.StartLevel);
        player.Inventory[InventoryConstants.InventoryStart].IsEmpty.Should().BeTrue();
        player.MaxHp.Should().Be((short)new ManesSurvivalSettings().StartHp);
        player.SavedLevel.Should().Be(RealLevel);
        player.SavedExperience.Should().Be(RealExperience);
        var saved = Bag();
        UserSessionBinaryState.LoadItems(saved, player.SavedItems);
        saved[InventoryConstants.InventoryStart].ItemId.Should().Be(BagItem);

        var character = new Character();
        provider.GetRequiredService<IUserSessionCharacterMapper>().ApplyToCharacter(player, character);
        character.Level.Should().Be(RealLevel);
        character.Experience.Should().Be(RealExperience);

        var start = Last(sent, ManesSurvivalSubOpcode.State, ManesStateKind.Start)!;
        start.ReadByte().Should().Be(ManesSurvivalRules.KarusWarriorRow);
        start.ReadShort().Should().Be(ManesSurvivalRules.DurationSeconds);
        start.ReadShort().Should().Be((short)new ManesSurvivalSettings().ExpToSecondLevel);
        start.ReadShort().Should().Be(0);
        start.ReadByte().Should().Be(ManesSurvivalRules.StartLevel);
    }

    [Fact]
    public async Task NoPlayerCanBeAttackedForTheFirstThreeMinutes()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = (ManesSurvivalService)provider.GetRequiredService<IManesSurvivalService>();
        var a = Player(sessions, 500, AccountNation.Karus);
        var b = Player(sessions, 501, AccountNation.Karus);

        await manes.StartAsync([500, 501], ManesSurvivalRules.DurationSeconds);
        var arena = manes.Arenas.Single();

        PvpRules.IsHostile(a, b).Should().BeFalse();
        ManesSurvivalRules.Hostile(a, b, arena.BattleOpenUtc.AddSeconds(-1)).Should().BeFalse();
        ManesSurvivalRules.Hostile(a, b, arena.BattleOpenUtc).Should().BeTrue("the same nation fights once the calm is over");
        (arena.BattleOpenUtc - arena.StartUtc).TotalSeconds.Should().Be(ManesSurvivalRules.CalmSeconds);
    }

    [Fact]
    public async Task AKillScoresAndTheRankingListsTheHunters()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var sentToB = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus);
        var b = Player(sessions, 501, AccountNation.ElMorad, sentToB);

        await manes.StartAsync([500, 501], ManesSurvivalRules.DurationSeconds);
        await manes.OnPlayerKilledAsync(b, a);

        int points = new ManesSurvivalSettings().KillScore;
        a.ManesSurvival!.Kills.Should().Be(1);
        a.ManesSurvival.Score.Should().Be(points);
        var line = Last(sentToB, ManesSurvivalSubOpcode.Notice, ManesNoticeKind.Kill)!;
        line.ReadByte().Should().Be((byte)AccountNation.Karus);
        line.ReadString().Should().Be(a.Name);
        line.ReadByte().Should().Be((byte)AccountNation.ElMorad);
        line.ReadString().Should().Be(b.Name);
        line.ReadUInt().Should().Be((uint)points);

        var ranking = manes.BuildRanking(b);
        ranking.ReadByte().Should().Be((byte)ManesSurvivalSubOpcode.State);
        ranking.ReadByte().Should().Be((byte)ManesStateKind.Rank);
        ranking.ReadByte().Should().Be(ManesSurvivalRules.Unranked);
        ranking.ReadShort().Should().Be(0);
        ranking.ReadUInt().Should().Be(0u);
        ranking.ReadByte().Should().Be(2);
        ranking.ReadByte().Should().Be((byte)AccountNation.Karus);
        ranking.ReadShort();
        ranking.ReadString().Should().BeEmpty();
        ranking.ReadString().Should().Be(a.Name);
        ranking.ReadShort().Should().Be(1);
        ranking.ReadUInt().Should().Be((uint)points);
    }

    [Fact]
    public async Task TheEndGivesBackTheBagAndLevelAndPaysTheRank()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);
        var b = Player(sessions, 501, AccountNation.Karus);

        await manes.StartAsync([500, 501], ManesSurvivalRules.DurationSeconds);
        Arrived(a, b);
        await manes.OnPlayerKilledAsync(b, a);
        await manes.FinishAllAsync();

        manes.HasActiveArenas.Should().BeFalse();
        a.ManesSurvival.Should().BeNull();
        a.ZoneId.Should().Be((byte)ZoneId.Moradon);
        a.Level.Should().Be(RealLevel);
        a.Experience.Should().Be(RealExperience);
        a.Inventory[InventoryConstants.InventoryStart].ItemId.Should().Be(BagItem);
        a.Inventory.Should().Contain(slot => slot.ItemId == FirstPrize);
        b.Inventory.Should().NotContain(slot => slot.ItemId == FirstPrize, "a hunter without points takes no rank");

        var end = Last(sent, ManesSurvivalSubOpcode.State, ManesStateKind.End)!;
        end.ReadLong();
        end.ReadLong().Should().Be(RealExperience);
        end.ReadByte().Should().Be(RealLevel);
        end.ReadInt().Should().Be(BagItem);

        var reward = Last(sent, ManesSurvivalSubOpcode.Notice, ManesNoticeKind.Reward)!;
        reward.ReadByte().Should().Be(1);
        reward.ReadUInt().Should().Be((uint)new ManesSurvivalSettings().KillScore);
        reward.ReadInt().Should().Be(FirstPrize);
    }

    [Fact]
    public async Task LeavingTheArenaEndsTheEventForThatPlayer()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);
        var b = Player(sessions, 501, AccountNation.Karus, sent);

        await manes.StartAsync([500, 501], ManesSurvivalRules.DurationSeconds);
        Arrived(a, b);
        await provider.GetRequiredService<IZoneTransitionService>().ChangeZoneAsync(a, (byte)ZoneId.Moradon, 800, 400);
        await manes.OnPlayerLogoutAsync(b);

        a.ManesSurvival.Should().BeNull();
        a.InstanceReturn.Should().BeNull();
        a.Level.Should().Be(RealLevel);
        Last(sent, ManesSurvivalSubOpcode.State, ManesStateKind.End).Should().NotBeNull();
        b.ManesSurvival.Should().BeNull();
        b.Inventory[InventoryConstants.InventoryStart].ItemId.Should().Be(BagItem);
        sent.Count(p => IsSub(p, ManesSurvivalSubOpcode.State, ManesStateKind.End)).Should().Be(1, "a logout sends nothing");
    }

    [Fact]
    public async Task TheSignUpWindowAnswersWithTheTwentySixNineteenCodes()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var scheduler = provider.GetRequiredService<EventSchedulerService>();
        var coordinator = provider.GetRequiredService<IManesSurvivalPacketCoordinator>();
        var sent = new List<Packet>();
        var player = Player(sessions, 500, AccountNation.Karus, sent);
        var young = Player(sessions, 501, AccountNation.Karus, sent);
        young.Level = ManesSurvivalRules.DefaultMinLevel - 1;

        await scheduler.CallTempleEventAsync(TempleEvent.ManesSurvival, 60, minLevel: ManesSurvivalRules.DefaultMinLevel);
        Last(sent, ManesSurvivalSubOpcode.SignUp, ManesSignUpKind.Open)!.ReadUShort().Should().Be(60);

        await coordinator.HandleAsync(player.Client, SignUp(ManesSignUpAction.Apply));
        var applied = Last(sent, ManesSurvivalSubOpcode.SignUp, ManesSignUpKind.Entry)!;
        applied.ReadShort().Should().Be((short)ManesSignUpResult.Applied);
        applied.ReadUShort().Should().Be(1);

        await coordinator.HandleAsync(young.Client, SignUp(ManesSignUpAction.Apply));
        Last(sent, ManesSurvivalSubOpcode.SignUp, ManesSignUpKind.Entry)!.ReadShort().Should().Be((short)ManesSignUpResult.LevelTooLow);

        scheduler.SignUpFor(player).Should().Be(TempleEvent.None, "the Chaos and BDW board never joins this event");

        await coordinator.HandleAsync(player.Client, SignUp(ManesSignUpAction.Cancel));
        Last(sent, ManesSurvivalSubOpcode.SignUp, ManesSignUpKind.Entry)!.ReadShort().Should().Be((short)ManesSignUpResult.Cancelled);
        scheduler.SignUpsOf(TempleEvent.ManesSurvival).Should().BeEmpty();
    }

    [Fact]
    public async Task AFullEventRefusesTheNextHunter()
    {
        using var provider = Provider(s => s.ManesSurvival.PlayersPerArena = 1);
        var sessions = provider.GetRequiredService<SessionManager>();
        var scheduler = provider.GetRequiredService<EventSchedulerService>();
        var coordinator = provider.GetRequiredService<IManesSurvivalPacketCoordinator>();
        var sent = new List<Packet>();
        await scheduler.CallTempleEventAsync(TempleEvent.ManesSurvival, 60, minLevel: ManesSurvivalRules.DefaultMinLevel);

        for (int id = 500; id < 500 + ManesSurvivalRules.ArenaCount; id++)
            await coordinator.HandleAsync(Player(sessions, id, AccountNation.Karus).Client, SignUp(ManesSignUpAction.Apply));
        await coordinator.HandleAsync(Player(sessions, 600, AccountNation.Karus, sent).Client, SignUp(ManesSignUpAction.Apply));

        Last(sent, ManesSurvivalSubOpcode.SignUp, ManesSignUpKind.Entry)!.ReadShort().Should().Be((short)ManesSignUpResult.Full);
    }

    [Fact]
    public void ExpCarriesOverLevelsAndStopsAtThirty()
    {
        var settings = new ManesSurvivalSettings();

        ManesSurvivalRules.Gain(settings, 1, 0, 19).Should().Be(((byte)1, 19L, 0));
        ManesSurvivalRules.Gain(settings, 1, 0, 20).Should().Be(((byte)2, 0L, 1));
        ManesSurvivalRules.Gain(settings, 1, 5, 50).Should().Be(((byte)3, 5L, 2));
        ManesSurvivalRules.Gain(settings, ManesSurvivalRules.MaxLevel - 1, 0, 100_000).Should()
            .Be((ManesSurvivalRules.MaxLevel, 0L, 1));
    }

    [Fact]
    public void OnlyTheManesTreeCastsInTheArenas()
    {
        ChaosRules.SkillAllowedIn(ManesSurvivalRules.FirstZone, ManesSurvivalRules.SkillTree).Should().BeTrue();
        ChaosRules.SkillAllowedIn(ManesSurvivalRules.LastZone, 105).Should().BeFalse();
        ChaosRules.SkillAllowedIn((byte)ZoneId.Moradon, ManesSurvivalRules.SkillTree).Should().BeFalse();
    }

    [Fact]
    public async Task HuntersHitOnTheEventScale()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var a = Player(sessions, 500, AccountNation.Karus);
        var b = Player(sessions, 501, AccountNation.Karus);
        await manes.StartAsync([500, 501], ManesSurvivalRules.DurationSeconds);

        ManesSurvivalCombat.MeleeSkill(250).Should().Be(250);
        ManesSurvivalCombat.FromMonster(b, 500).Should().Be(500 / ManesSurvivalCombat.MonsterDamageDivisor);
        b.ManesSurvival!.AttackDown = 10;
        ManesSurvivalCombat.Hit(a, b, 100).Should().Be(90);
        a.AttackAmount = 200;
        ManesSurvivalCombat.Hit(a, b, 100).Should().Be(180, "an attack buff still counts");
        ManesSurvivalCombat.FromMonster(Player(sessions, 502, AccountNation.Karus), 500).Should().Be(500, "outside the event nothing changes");
    }

    [Fact]
    public async Task AMonsterKillLevelsUpAndOffersSkillsAndPotions()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);
        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);
        var settings = new ManesSurvivalSettings();

        await manes.OnMonsterKilledAsync(Monster(30), a);

        a.Level.Should().Be(2);
        a.ManesSurvival!.Exp.Should().Be(30 - settings.ExpToSecondLevel);
        a.ManesSurvival.Score.Should().Be(settings.MonsterScore + settings.LevelScore);
        a.MaxHp.Should().Be((short)(settings.StartHp + settings.HpPerLevel));
        var level = Last(sent, ManesSurvivalSubOpcode.Progress, ManesProgressKind.Level)!;
        level.ReadInt().Should().Be(a.CharacterId);
        level.ReadByte().Should().Be(2);

        var offer = Last(sent, ManesSurvivalSubOpcode.Pick, ManesPickKind.Offer)!;
        var skills = Codes(offer);
        var potions = Codes(offer);
        skills.Should().HaveCount(ManesSurvivalRules.OfferSize).And.OnlyHaveUniqueItems();
        skills.Should().OnlyContain(code => code % 100 == 1, "every group starts at its first level");
        potions.Should().BeEquivalentTo(new short[] { 6201, 6301 });
    }

    [Fact]
    public async Task APickTeachesTheSkillAndAPotionCostsRealNoah()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var table = provider.GetRequiredService<IManesSurvivalTable>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);
        a.Money = 5000;
        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);
        await manes.OnMonsterKilledAsync(Monster(30), a);
        var offer = a.ManesSurvival!.CurrentOffer!;

        await manes.PickAsync(a, ManesPickAction.Potion, 6201);
        PickResult(sent).Should().Be(ManesSurvivalRules.PickSucceeded);
        a.Money.Should().Be(2000);
        a.Inventory[InventoryConstants.InventoryStart].ItemId.Should().Be(HpPotion);
        a.Inventory[InventoryConstants.InventoryStart].Count.Should().Be(10);
        var put = Last(sent, ManesSurvivalSubOpcode.Bag, ManesBagKind.Put)!;
        put.ReadByte().Should().Be(0);
        put.ReadInt().Should().Be(HpPotion);

        await manes.PickAsync(a, ManesPickAction.Potion, 6201);
        PickResult(sent).Should().Be(ManesSurvivalRules.PickNotEnoughNoah);

        var code = offer.Skills[0];
        await manes.PickAsync(a, ManesPickAction.Skill, code);
        PickResult(sent).Should().Be(ManesSurvivalRules.PickSucceeded);
        a.ManesSurvival.Knows(table.Pick(code)!.Magic).Should().BeTrue();
        a.ManesSurvival.CurrentOffer.Should().BeNull();

        await manes.PickAsync(a, ManesPickAction.Skill, code);
        PickResult(sent).Should().Be(ManesSurvivalRules.PickFailed, "no offer is open any more");

        await manes.DiscardAsync(a, 0, HpPotion);
        Last(sent, ManesSurvivalSubOpcode.Bag, ManesBagKind.Discard)!.ReadByte().Should().Be(1);
        a.Inventory[InventoryConstants.InventoryStart].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task AHunterKillPaysExpByTheVictimsLevel()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var a = Player(sessions, 500, AccountNation.Karus);
        var b = Player(sessions, 501, AccountNation.Karus);
        await manes.StartAsync([500, 501], ManesSurvivalRules.DurationSeconds);
        var settings = new ManesSurvivalSettings();

        await manes.OnPlayerKilledAsync(b, a);

        a.Level.Should().Be(1);
        a.ManesSurvival!.Exp.Should().Be(settings.ExpPerHunterLevel * ManesSurvivalRules.StartLevel);
    }

    private static NpcInstance Monster(short level) => new()
    {
        NpcId = 1003, Level = level, ZoneId = ManesSurvivalRules.FirstZone, IsMonster = true,
    };

    private static List<short> Codes(Packet packet)
    {
        int count = packet.ReadByte();
        return Enumerable.Range(0, count).Select(_ => packet.ReadShort()).ToList();
    }

    private static short PickResult(List<Packet> sent)
    {
        var reply = Last(sent, ManesSurvivalSubOpcode.Pick, ManesPickKind.Choice)!;
        reply.ReadByte();
        return reply.ReadShort();
    }

    [Fact]
    public async Task TheBossStandsInvincibleForTheFirstTenMinutes()
    {
        var clock = new Clock();
        using var provider = Provider(clock: clock);
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = (ManesSurvivalService)provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);

        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);
        var boss = manes.Arenas.Single().Boss!;
        var settings = new ManesSurvivalSettings();

        boss.NpcId.Should().Be(settings.BossNpcId);
        boss.MaxHp.Should().Be(settings.BossHp);
        boss.IsInvincible.Should().BeTrue();
        boss.DamageTaken(500, basicHit: false).Should().Be(0);
        new DateTime(boss.InvincibleUntilTicks, DateTimeKind.Utc).Should()
            .Be(clock.GetUtcNow().UtcDateTime.AddSeconds(ManesSurvivalRules.BossInvincibleSeconds));

        var bar = Last(sent, ManesSurvivalSubOpcode.State, ManesStateKind.Boss)!;
        bar.ReadString().Should().Be(BossName);
        bar.ReadUInt().Should().Be((uint)settings.BossHp);
        bar.ReadUInt().Should().Be((uint)settings.BossHp);
        bar.ReadUInt().Should().Be((uint)ManesSurvivalRules.DurationSeconds);

        manes.NoteInvincibleHit(a);
        await manes.TickAsync();
        Last(sent, ManesSurvivalSubOpcode.Notice, ManesNoticeKind.Invincible).Should().NotBeNull();
    }

    [Fact]
    public async Task AMidBossGivesAPassiveAndABonusPick()
    {
        var clock = new Clock();
        using var provider = Provider(clock: clock);
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = (ManesSurvivalService)provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);
        var settings = new ManesSurvivalSettings();

        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);
        var arena = manes.Arenas.Single();
        clock.Advance(settings.MidBossWaveSeconds[0]);
        await manes.TickAsync();

        arena.MidBosses.Should().HaveCount(settings.MidBossesPerWave);
        var markers = Last(sent, ManesSurvivalSubOpcode.Notice, ManesNoticeKind.Markers)!;
        markers.ReadByte().Should().Be((byte)settings.MidBossesPerWave);

        var midBoss = arena.MidBosses[0];
        midBoss.MaxHp.Should().Be(settings.MidBossHp);
        await manes.OnMonsterKilledAsync(midBoss, a);

        arena.MidBosses.Should().HaveCount(settings.MidBossesPerWave - 1);
        var entry = a.ManesSurvival!;
        (entry.HpBonus + entry.AttackUp + entry.AttackDown).Should().BePositive();
        Last(sent, ManesSurvivalSubOpcode.Pick, ManesPickKind.Passive).Should().NotBeNull();
        var bonus = Codes(Last(sent, ManesSurvivalSubOpcode.Pick, ManesPickKind.BonusOffer)!);
        bonus.Should().NotBeEmpty();
        entry.CurrentOffer!.Bonus.Should().BeTrue();

        await manes.BonusPickAsync(a, ManesPickAction.Skill, bonus[0]);
        Last(sent, ManesSurvivalSubOpcode.Pick, ManesPickKind.BonusChoice)!.ReadShort().Should().Be(ManesSurvivalRules.PickSucceeded);
        (entry.CurrentOffer?.Bonus ?? false).Should().BeFalse("the level-up offers of the mid-boss exp queue behind the bonus");
    }

    [Fact]
    public async Task TheBossKillEndsTheArenaAndPaysTheKiller()
    {
        var clock = new Clock();
        using var provider = Provider(clock: clock);
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = (ManesSurvivalService)provider.GetRequiredService<IManesSurvivalService>();
        var sent = new List<Packet>();
        var a = Player(sessions, 500, AccountNation.Karus, sent);
        var settings = new ManesSurvivalSettings();

        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);
        Arrived(a);
        var arena = manes.Arenas.Single();
        var boss = arena.Boss!;
        boss.Hp = 0;
        await manes.OnMonsterKilledAsync(boss, a);

        Last(sent, ManesSurvivalSubOpcode.Notice, ManesNoticeKind.LastBoss)!.ReadString().Should().Be(a.Name);
        a.ManesSurvival!.Score.Should().BeGreaterThanOrEqualTo(settings.BossScore);

        clock.Advance(settings.EndAfterBossSeconds);
        await manes.TickAsync();

        manes.HasActiveArenas.Should().BeFalse();
        a.Inventory.Should().Contain(slot => slot.ItemId == settings.BossKillerItemId);
        var lines = sent.Where(p => IsSub(p, ManesSurvivalSubOpcode.Notice, ManesNoticeKind.Reward)).ToList();
        lines.Should().HaveCount(2);
        IsSub(lines[^1], ManesSurvivalSubOpcode.Notice, ManesNoticeKind.Reward);
        lines[^1].ReadByte();
        lines[^1].ReadUInt();
        lines[^1].ReadInt().Should().Be(settings.BossKillerItemId);
        lines[^1].ReadByte();
        lines[^1].ReadByte().Should().Be(1);
    }

    [Fact]
    public async Task ARealItemCarriedIntoTheEventBagComesBackAtTheEnd()
    {
        using var provider = Provider();
        var sessions = provider.GetRequiredService<SessionManager>();
        var manes = provider.GetRequiredService<IManesSurvivalService>();
        var a = Player(sessions, 500, AccountNation.Karus);
        await manes.StartAsync([500], ManesSurvivalRules.DurationSeconds);
        Arrived(a);
        var carried = a.Inventory[InventoryConstants.InventoryStart + 5];
        carried.ItemId = FirstPrize;
        carried.Count = 1;
        carried.Durability = 1;
        var potion = a.Inventory[InventoryConstants.InventoryStart + 6];
        potion.ItemId = HpPotion;
        potion.Count = 10;

        await manes.FinishAllAsync();

        a.Inventory[InventoryConstants.InventoryStart].ItemId.Should().Be(BagItem);
        a.Inventory.Should().Contain(slot => slot.ItemId == FirstPrize, "only event items stay behind");
        a.Inventory.Should().NotContain(slot => slot.ItemId == HpPotion);
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }

    private static ServiceProvider Provider(Action<GameServerSettings>? configureSettings = null, TimeProvider? clock = null) =>
        CreateProvider(_ => { }, gameData =>
        {
            var settings = new ManesSurvivalSettings();
            gameData.GetCoefficient(Arg.Any<short>()).Returns(new CoefficientData());
            gameData.GetItem(FirstPrize).Returns(new ItemData { Num = FirstPrize, Duration = 1 });
            gameData.GetItem(HpPotion).Returns(new ItemData { Num = HpPotion, Duration = 1, Countable = 1 });
            gameData.GetItem(BossKillerItem).Returns(new ItemData { Num = BossKillerItem, Duration = 1 });
            gameData.GetNpc(settings.BossNpcId).Returns(new NpcData { Id = settings.BossNpcId, Name = BossName, Level = 100, Hp = 20654, IsMonster = true });
            gameData.GetNpc(settings.MidBossNpcId).Returns(new NpcData { Id = settings.MidBossNpcId, Name = "Scorpion King", Level = 60, Hp = 6000, IsMonster = true });
        }, configureSettings, services =>
        {
            if (clock != null)
                services.AddSingleton(clock);
        });

    private static Packet SignUp(ManesSignUpAction action)
    {
        var packet = new Packet(GameOpcodes.GS_MANES_SURVIVAL);
        packet.WriteByte((byte)ManesSurvivalSubOpcode.SignUp);
        packet.WriteByte((byte)ManesSignUpKind.Entry);
        packet.WriteUShort((ushort)action);
        packet.ResetOffset();
        return packet;
    }

    private static bool IsSub<TKind>(Packet packet, ManesSurvivalSubOpcode sub, TKind kind) where TKind : Enum
    {
        if (packet.GetOpcode() != (byte)GameOpcodes.GS_MANES_SURVIVAL)
            return false;
        packet.ResetOffset();
        return packet.ReadByte() == (byte)sub && packet.ReadByte() == Convert.ToByte(kind);
    }

    private static Packet? Last<TKind>(List<Packet> sent, ManesSurvivalSubOpcode sub, TKind kind) where TKind : Enum
    {
        var packet = sent.LastOrDefault(p => IsSub(p, sub, kind));
        if (packet == null)
            return null;
        IsSub(packet, sub, kind);
        return packet;
    }

    private static void Arrived(params UserSession[] players)
    {
        foreach (var player in players)
            player.IsWarping = false;
    }

    private static ItemSlot[] Bag()
    {
        var bag = new ItemSlot[InventoryConstants.InventoryTotal];
        for (int i = 0; i < bag.Length; i++)
            bag[i] = new ItemSlot();
        return bag;
    }

    private static UserSession Player(SessionManager sessions, int id, AccountNation nation, List<Packet>? sent = null)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(sent == null ? Arg.Any<Packet>() : Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, characterId: id, accountId: id + 1);
        session.Name = $"P{id}";
        session.Nation = nation;
        session.Class = nation == AccountNation.Karus ? (short)101 : (short)201;
        session.Level = RealLevel;
        session.Experience = RealExperience;
        session.ZoneId = (byte)ZoneId.Moradon;
        session.X = 800;
        session.Z = 400;
        session.Inventory[InventoryConstants.InventoryStart].ItemId = BagItem;
        session.Inventory[InventoryConstants.InventoryStart].Count = 3;
        session.Inventory[InventoryConstants.InventoryStart].Durability = 1;
        sessions.Regions.AddToRegion(session);
        return session;
    }
}
