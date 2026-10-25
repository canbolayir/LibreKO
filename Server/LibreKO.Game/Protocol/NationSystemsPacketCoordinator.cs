using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.Protocol;

public interface INationSystemsPacketCoordinator
{
    Task HandleBifrostAsync(IClient client, Packet packet);
    Task HandleRankAsync(IClient client, Packet packet);
    Task HandleSiegeAsync(IClient client, Packet packet);
    Task HandleKingAsync(IClient client, Packet packet);
}

public class NationSystemsPacketCoordinator(
    SessionManager sessionManager,
    IGameDataService gameDataService,
    IKingElectionPacketService kingElectionPacketService,
    IKingGovernancePacketService kingGovernancePacketService,
    IBifrostEventService bifrostEventService,
    IBorderDefenseWarService borderDefenseWarService,
    IChaosDungeonService chaosDungeonService,
    EventSchedulerService eventSchedulerService,
    INationRankService nationRanks,
    ICastleSiegeService castleSiegeService,
    ILogger<NationSystemsPacketCoordinator> logger) : INationSystemsPacketCoordinator
{
    public async Task HandleBifrostAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || session.Hp <= 0 || packet.RemainingBytes < 1)
            return;

        //   2 = BIFROST_EVENT    — remaining time query (the only one with logic)
        var sub = packet.ReadByte();
        if (sub != (byte)TempleSubOpcode.BifrostRemaining)
            return;

        bool signUpSent = false;
        foreach (var run in eventSchedulerService.TempleEvents)
        {
            if (!run.JoinOpen || !run.Fits(session.Level))
                continue;
            await session.Client.SendPacket(EventPacketWriter.SignUpTime(run.Contest, run.SecondsLeft));
            signUpSent = true;
        }

        var bifrostSecs = session.ZoneId is BifrostRules.ZoneRonarkLand or BifrostRules.ZoneBifrost
            ? (int)Math.Min(bifrostEventService.RemainingSecs, int.MaxValue)
            : 0;
        if (bifrostSecs > 0 || !signUpSent)
            await session.Client.SendPacket(BifrostPacketWriter.Remaining(
                TempleSubOpcode.BifrostRemaining, bifrostSecs));
    }

    private const byte RankTypePkZone = 1;
    private const byte RankTypeBorderDefenseWar = 2;
    private const byte RankTypeChaosDungeon = 3;
    private const int PkZoneTopCount = 10;

    public async Task HandleRankAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1)
            return;

        var rankType = packet.ReadByte();

        var response = rankType switch
        {
            RankTypePkZone => BuildPkZoneRank(session, rankType),
            RankTypeBorderDefenseWar => borderDefenseWarService.BuildRanking(session, rankType),
            RankTypeChaosDungeon => chaosDungeonService.BuildRanking(session, rankType),
            _ => RankPacketWriter.Unsupported(rankType),
        };
        await session.Client.SendPacket(response);
    }

    private Packet BuildPkZoneRank(UserSession session, byte rankType)
    {
        var karus = ZoneLadder(session, AccountNation.Karus);
        var elmorad = ZoneLadder(session, AccountNation.ElMorad);
        var own = session.Nation == AccountNation.Karus ? karus : elmorad;
        var myRank = own.FindIndex(entry => entry.CharacterId == session.CharacterId) + 1;

        return RankPacketWriter.PkZone(
            rankType,
            karus.Take(PkZoneTopCount).Select(ToRankEntry).ToList(),
            elmorad.Take(PkZoneTopCount).Select(ToRankEntry).ToList(),
            (ushort)Math.Min(myRank, ushort.MaxValue),
            session.DailyLoyalty,
            RankPacketWriter.PremiumShare(session.DailyPremiumLoyalty));
    }

    private List<UserSession> ZoneLadder(UserSession viewer, AccountNation nation) =>
        sessionManager.GetAll()
            .Where(s => s.Nation == nation && s.ZoneId == viewer.ZoneId && s.Room == viewer.Room)
            .OrderByDescending(s => s.DailyLoyalty)
            .ThenBy(s => s.CharacterId)
            .ToList();

    private RankPacketWriter.RankEntry ToRankEntry(UserSession session)
    {
        var clan = session.KnightsId > 0 ? sessionManager.Knights.GetClan(session.KnightsId) : null;
        var ranks = nationRanks.Of(session.CharacterId);
        return new RankPacketWriter.RankEntry(
            session.Name,
            (byte)session.Nation,
            clan == null ? (ushort)0 : (ushort)session.KnightsId,
            clan == null || clan.MarkVersion < 0 ? (ushort)0 : (ushort)clan.MarkVersion,
            clan?.Name ?? string.Empty,
            session.DailyLoyalty,
            RankPacketWriter.PremiumShare(session.DailyPremiumLoyalty),
            Math.Min(ranks.Knights, ranks.Personal));
    }

    public async Task HandleSiegeAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || session.Hp <= 0 || packet.RemainingBytes < 1)
            return;

        var opcode = packet.ReadByte();
        switch (opcode)
        {
            case SiegePacketConstants.BaseCreate: await HandleSiegeBaseCreateAsync(session, ReadSubType(packet)); break;
            case SiegePacketConstants.CastleFlag: await session.Client.SendPacket(castleSiegeService.StatusPacket()); break;
            case SiegePacketConstants.WarfareNpc: await HandleWarfareNpcAsync(session, packet); break;
            case SiegePacketConstants.CastleManager: await HandleCastleManagerAsync(session, packet); break;
            case SiegePacketConstants.Rank: await HandleSiegeRankAsync(session, ReadSubType(packet)); break;
        }
    }

    private static byte ReadSubType(Packet packet) => packet.RemainingBytes >= 1 ? packet.ReadByte() : (byte)0;

    private static async Task HandleSiegeBaseCreateAsync(UserSession session, byte subType)
    {
        var resp = SiegePacketWriter.Result(SiegePacketConstants.BaseCreate, subType);
        await session.Client.SendPacket(resp);
    }

    private async Task HandleWarfareNpcAsync(UserSession session, Packet packet)
    {
        var siege = gameDataService.SiegeWarfare;
        if (siege == null || packet.RemainingBytes < 1)
            return;

        var subType = packet.ReadByte();
        switch (subType)
        {
            case SiegePacketConstants.WarfareApply:
                await session.Client.SendPacket(SiegePacketWriter.Result(
                    SiegePacketConstants.WarfareNpc, SiegePacketConstants.WarfareApply, SiegePacketConstants.SignUpClosed));
                break;

            case SiegePacketConstants.WarfareSchedule:
                await session.Client.SendPacket(SiegePacketWriter.Schedule(ScheduledWars(siege)));
                break;

            case SiegePacketConstants.WarfareAssault:
                logger.LogInformation("{Name} asked the castle guard for an assault; no reply is defined", session.Name);
                break;

            case SiegePacketConstants.WarfareChallengers:
                await SendChallengersAsync(session, siege);
                break;

            case SiegePacketConstants.WarfareDefendingUnion:
                await session.Client.SendPacket(SiegePacketWriter.DefendingUnion(DefendingClans(siege)));
                break;
        }
    }

    private static List<SiegePacketWriter.WarSchedule> ScheduledWars(SiegeWarfareData siege) =>
        SiegeRules.IsWeekday(siege.WarDay)
            ? [new SiegePacketWriter.WarSchedule(siege.SiegeType, siege.WarDay, siege.WarTime, siege.WarMinute)]
            : [];

    private async Task SendChallengersAsync(UserSession session, SiegeWarfareData siege)
    {
        if (!SiegeRules.IsWeekday(siege.WarRequestDay))
        {
            await session.Client.SendPacket(SiegePacketWriter.Result(
                SiegePacketConstants.WarfareNpc, SiegePacketConstants.WarfareChallengers, SiegePacketConstants.SignUpClosed));
            return;
        }

        short[] requests =
        [
            siege.RequestList1, siege.RequestList2, siege.RequestList3, siege.RequestList4, siege.RequestList5,
            siege.RequestList6, siege.RequestList7, siege.RequestList8, siege.RequestList9, siege.RequestList10,
        ];

        var period = new SiegePacketWriter.RegistrationPeriod(
            siege.WarRequestDay, siege.WarRequestTime, siege.WarRequestMinute,
            SiegeRules.IsWeekday(siege.WarDay) ? SiegeRules.DayBefore(siege.WarDay) : siege.WarRequestDay);

        await session.Client.SendPacket(SiegePacketWriter.Challengers(
            ClanRows(requests), SiegeRules.ChallengerSlots, SiegeRules.ChallengerSignUpFee, period));
    }

    private List<SiegePacketWriter.ClanRow> DefendingClans(SiegeWarfareData siege)
    {
        if (siege.MasterKnights <= 0)
            return [];

        return ClanRows([siege.MasterKnights, .. sessionManager.Knights.GetAllianceClanIds(siege.MasterKnights)]);
    }

    private List<SiegePacketWriter.ClanRow> ClanRows(IEnumerable<short> clanIds) =>
        clanIds
            .Where(clanId => clanId > 0)
            .Distinct()
            .Select(clanId => sessionManager.Knights.GetClan(clanId))
            .OfType<KnightsEntity>()
            .Select(clan => new SiegePacketWriter.ClanRow(
                clan.Name, clan.Nation, (byte)Math.Clamp((int)clan.Members, 0, byte.MaxValue)))
            .ToList();

    private async Task HandleCastleManagerAsync(UserSession session, Packet packet)
    {
        var siege = gameDataService.SiegeWarfare;
        if (siege == null || packet.RemainingBytes < 1)
            return;

        var subType = packet.ReadByte();
        if (!SiegeRules.IsCastleLord(session, siege))
        {
            await session.Client.SendPacket(SiegePacketWriter.Result(
                SiegePacketConstants.CastleManager, subType, SiegePacketConstants.NoAuthority));
            return;
        }

        switch (subType)
        {
            case SiegePacketConstants.ManagerCollect:
                await CollectDungeonChargeAsync(session, siege);
                break;

            case SiegePacketConstants.ManagerTariffs:
                await session.Client.SendPacket(SiegePacketWriter.Tariffs(
                    siege.MoradonTariff, siege.DellosTariff, siege.DungeonEntranceFee));
                break;

            case SiegePacketConstants.ManagerMoradonTariff:
                await ChangeTariffAsync(session, packet, subType, ZoneId.Moradon, rate => siege.MoradonTariff = rate);
                break;

            case SiegePacketConstants.ManagerDelosTariff:
                await ChangeTariffAsync(session, packet, subType, ZoneId.Delos, rate => siege.DellosTariff = rate);
                break;

            case SiegePacketConstants.ManagerDungeonFee:
                await ChangeDungeonFeeAsync(session, packet, siege);
                break;
        }
    }

    private async Task CollectDungeonChargeAsync(UserSession session, SiegeWarfareData siege)
    {
        var charge = Math.Max(siege.DungeonCharge, 0);
        if ((long)session.Money + charge > ExchangePacketConstants.CoinMax)
        {
            await session.Client.SendPacket(SiegePacketWriter.Result(
                SiegePacketConstants.CastleManager, SiegePacketConstants.ManagerCollect, SiegePacketConstants.CoinsFull));
            return;
        }

        session.Money += charge;
        siege.DungeonCharge = 0;
        logger.LogInformation("{Name} collected {Charge} coins of Delos dungeon charge", session.Name, charge);

        await session.Client.SendPacket(SiegePacketWriter.TaxCollected(session.Money, charge));
    }

    private async Task ChangeTariffAsync(UserSession session, Packet packet, byte subType, ZoneId zone, Action<short> apply)
    {
        if (packet.RemainingBytes < sizeof(ushort))
            return;

        var rate = packet.ReadUShort();
        if (rate > SiegeRules.MaxTariff)
        {
            await session.Client.SendPacket(SiegePacketWriter.Result(
                SiegePacketConstants.CastleManager, subType, SiegePacketConstants.RateNotAllowed));
            return;
        }

        apply((short)rate);
        await sessionManager.BroadcastToAll(SiegePacketWriter.TariffChanged(subType, (short)rate, (short)zone));
        logger.LogInformation("{Name} set the {Zone} tariff to {Tariff}", session.Name, zone, rate);
    }

    private async Task ChangeDungeonFeeAsync(UserSession session, Packet packet, SiegeWarfareData siege)
    {
        if (packet.RemainingBytes < sizeof(uint))
            return;

        var fee = packet.ReadUInt();
        if (fee > ExchangePacketConstants.CoinMax)
        {
            await session.Client.SendPacket(SiegePacketWriter.Result(
                SiegePacketConstants.CastleManager, SiegePacketConstants.ManagerDungeonFee, SiegePacketConstants.RateNotAllowed));
            return;
        }

        siege.DungeonEntranceFee = (int)fee;
        logger.LogInformation("{Name} set the Delos dungeon entrance fee to {Fee}", session.Name, fee);
        await session.Client.SendPacket(SiegePacketWriter.DungeonFeeChanged(siege.DungeonEntranceFee));
    }

    private async Task HandleSiegeRankAsync(UserSession session, byte subType)
    {
        var resp = SiegePacketWriter.RankList(SiegePacketConstants.Rank, subType, 0);
        await session.Client.SendPacket(resp);
    }

    public async Task HandleKingAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1)
            return;

        var subOpcode = packet.ReadByte();
        switch (subOpcode)
        {
            case KingPacketConstants.Election:
                await kingElectionPacketService.HandleElectionAsync(session, packet);
                break;

            case KingPacketConstants.Impeachment:
                await kingElectionPacketService.HandleImpeachmentAsync(session, packet);
                break;

            case KingPacketConstants.Tax:
                await kingGovernancePacketService.HandleTaxAsync(session, packet);
                break;

            case KingPacketConstants.Event:
                await kingGovernancePacketService.HandleKingEventAsync(session, packet);
                break;

            case KingPacketConstants.NationIntro:
                await kingGovernancePacketService.HandleNationIntroAsync(session, packet);
                break;
        }
    }
}
