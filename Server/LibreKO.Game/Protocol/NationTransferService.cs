using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using System.Text;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace LibreKO.Game.Protocol;

public interface INationTransferService
{
    Task OpenAsync(UserSession session);
    Task HandleAsync(IClient client, Packet packet);
}

public class NationTransferService(
    IServiceScopeFactory scopeFactory,
    SessionManager sessionManager,
    IGameDataService gameDataService,
    IMagicItemUsageService magicItemUsageService,
    IKingSystemRuntimeService kingSystemRuntimeService,
    ISessionTerminationService sessionTermination,
    IUserNotificationService userNotificationService,
    ILogger<NationTransferService> logger) : INationTransferService
{
    public const int NationTransferItem = 810096000;
    private const byte Moradon = (byte)ZoneId.Moradon;
    private const float MoradonTownX = 816f;
    private const float MoradonTownZ = 532f;
    private const int RequestTailLength = 6;

    private sealed record Request(short Slot, string Name, byte Race, byte Face, int Hair);

    public async Task OpenAsync(UserSession session)
    {
        if (await RefusedDuringWarAsync(session))
            return;

        var (refusal, characters) = await CheckAsync(session);
        if (refusal != NationTransferPacketWriter.Accepted)
        {
            await session.Client.SendPacket(NationTransferPacketWriter.Result(NationTransferPacketWriter.OpenBox, refusal));
            return;
        }

        var newNation = NationTransferRules.OtherNation(session.Nation);
        var candidates = characters
            .OrderBy(character => character.Slot)
            .Select(character => new NationTransferCandidate(
                character.Slot,
                character.Name,
                NationTransferRules.ProposedRace(character.Race, character.Class),
                newNation,
                NationTransferRules.NewClass(character.Class),
                character.Face,
                character.Hair))
            .ToList();
        await session.Client.SendPacket(NationTransferPacketWriter.Candidates(candidates));
    }

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1)
            return;

        switch (packet.ReadByte())
        {
            case NationTransferPacketWriter.OpenBox when packet.RemainingBytes == 0:
                await OpenAsync(session);
                break;
            case NationTransferPacketWriter.Submit when packet.RemainingBytes >= 1
                                                       && packet.ReadByte() == NationTransferPacketWriter.Accepted:
                await SubmitAsync(session, ReadRequests(packet));
                break;
        }
    }

    private static List<Request>? ReadRequests(Packet packet)
    {
        if (packet.RemainingBytes == 0)
            return null;
        var count = packet.ReadByte();
        if (count == 0)
            return null;
        var requests = new List<Request>(count);
        var slots = new HashSet<short>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < count; index++)
        {
            if (packet.RemainingBytes < 4)
                return null;
            var slot = packet.ReadShort();
            var nameLength = packet.ReadShort();
            if (slot < 0 || !slots.Add(slot) || nameLength is <= 0 or > 20
                || packet.RemainingBytes < nameLength + RequestTailLength)
                return null;
            var name = Encoding.ASCII.GetString(packet.ReadBytes(nameLength));
            if (!names.Add(name))
                return null;
            requests.Add(new Request(slot, name, packet.ReadByte(), packet.ReadByte(), packet.ReadInt()));
        }
        return packet.RemainingBytes == 0 ? requests : null;
    }

    private async Task<bool> RefusedDuringWarAsync(UserSession session)
    {
        var battle = sessionManager.Battle;
        if (!battle.IsBattleActive)
            return false;
        await session.Client.SendPacket(NationTransferPacketWriter.DuringWar(battle.KilledElmoNpc, battle.KilledKarusNpc));
        return true;
    }

    private async Task SubmitAsync(UserSession session, List<Request>? requests)
    {
        if (await RefusedDuringWarAsync(session))
            return;

        var (refusal, characters) = await CheckAsync(session);
        if (refusal == NationTransferPacketWriter.Accepted && !Matches(characters, requests))
            refusal = NationTransferPacketWriter.WrongCharacter;
        if (refusal != NationTransferPacketWriter.Accepted)
        {
            await session.Client.SendPacket(NationTransferPacketWriter.Result(NationTransferPacketWriter.Submit, refusal));
            return;
        }

        var oldNation = session.Nation;
        var newNation = NationTransferRules.OtherNation(oldNation);
        var bySlot = requests!.ToDictionary(request => request.Slot);
        var playing = characters.Single(character => character.Id == session.CharacterId);
        var own = bySlot[playing.Slot];
        byte result = NationTransferPacketWriter.Failed;
        await session.CharacterPersistenceGate.WaitAsync();
        try
        {
            if (session.NationTransferCommitted || sessionManager.GetByClientId(session.Client.Id) != session)
                return;
            // The connection processes packets serially; serialize autosaves with this account migration too.
            var before = session.SerializeItems();
            try
            {
                if (!await magicItemUsageService.TryConsumeItemAsync(session, NationTransferItem))
                    result = NationTransferPacketWriter.NoItem;
                else if (await PersistAsync(session, newNation, characters, bySlot))
                {
                    session.WithLock(active =>
                    {
                        active.Nation = newNation;
                        active.Class = NationTransferRules.NewClass(active.Class);
                        active.Race = own.Race;
                        active.Face = own.Face;
                        active.Hair = own.Hair;
                        active.Quest.BindPoint = -1;
                        active.NationTransferCommitted = true;
                    });
                    result = NationTransferPacketWriter.Accepted;
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not persist nation transfer for account {Account}", session.AccountId);
            }
            if (result != NationTransferPacketWriter.Accepted)
                await RestoreCertificateAsync(session, before);
        }
        finally
        {
            session.CharacterPersistenceGate.Release();
        }

        if (result != NationTransferPacketWriter.Accepted)
        {
            await session.Client.SendPacket(NationTransferPacketWriter.Result(NationTransferPacketWriter.Submit, result));
            return;
        }
        await sessionTermination.LogoutAsync(session.Client);
        await session.Client.SendPacket(NationTransferPacketWriter.Result(NationTransferPacketWriter.Submit, result));

        logger.LogInformation("Nation transfer for account {Account}: {Count} characters {Old}→{New}",
            session.AccountId, characters.Count, oldNation, newNation);
    }

    private static bool Matches(IReadOnlyList<Character> characters, List<Request>? requests) =>
        requests != null
        && requests.Count == characters.Count
        && requests.Select(request => request.Slot).Distinct().Count() == requests.Count
        && requests.All(request => characters.FirstOrDefault(character => character.Slot == request.Slot) is { } character
                                   && string.Equals(character.Name, request.Name, StringComparison.Ordinal)
                                   && NationTransferRules.Allows(character.Class, request.Race));

    private async Task<(byte Refusal, IReadOnlyList<Character> Characters)> CheckAsync(UserSession session)
    {
        if (session.Hp <= 0 || session.Trade.IsTrading || session.Trade.IsMerchanting
            || session.Trade.IsMerchantPreparing || session.IsGathering || session.NationTransferCommitted)
            return (NationTransferPacketWriter.Failed, []);
        if (!magicItemUsageService.CanUseItem(session, NationTransferItem))
            return (NationTransferPacketWriter.NoItem, []);

        using var scope = scopeFactory.CreateScope();
        var characters = (await scope.ServiceProvider.GetRequiredService<ICharacterRepository>()
            .GetCharactersByAccount(session.AccountId)).ToList();
        if (characters.Count == 0 || characters.Count > byte.MaxValue
            || characters.All(character => character.Id != session.CharacterId))
            return (NationTransferPacketWriter.NoCharacter, characters);
        if (session.KnightsId > 0 || characters.Any(character => character.KnightsId > 0))
            return (NationTransferPacketWriter.InClan, characters);
        var king = kingSystemRuntimeService.GetKingData(session.Nation)?.KingName?.Trim();
        if (!string.IsNullOrEmpty(king)
            && characters.Any(character => string.Equals(character.Name, king, StringComparison.OrdinalIgnoreCase)))
            return (NationTransferPacketWriter.IsKing, characters);
        return (NationTransferPacketWriter.Accepted, characters);
    }

    private async Task<bool> PersistAsync(UserSession session, AccountNation newNation, IReadOnlyList<Character> characters,
        IReadOnlyDictionary<short, Request> bySlot)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Accounts.FindAsync(session.AccountId);
        var current = await db.Characters.Where(character => character.AccountId == session.AccountId).ToListAsync();
        if (account == null || account.Nation != session.Nation || current.Count != characters.Count
            || current.Any(character => character.KnightsId > 0
                || !characters.Any(listed => listed.Id == character.Id && listed.Slot == character.Slot
                    && listed.Class == character.Class && listed.Name == character.Name))
            || !Matches(current, bySlot.Values.ToList()))
            return false;
        var active = current.SingleOrDefault(character => character.Id == session.CharacterId);
        if (active == null)
            return false;
        scope.ServiceProvider.GetRequiredService<IUserSessionCharacterMapper>().ApplyToCharacter(session, active);
        var start = gameDataService.GetStartPosition(Moradon);

        foreach (var character in current)
        {
            var request = bySlot[character.Slot];
            var (x, z) = start?.RandomSpawn(newNation) ?? (MoradonTownX, MoradonTownZ);
            character.Class = NationTransferRules.NewClass(character.Class);
            character.Race = request.Race;
            character.Face = request.Face;
            character.Hair = request.Hair;
            character.MapId = Moradon;
            character.X = x;
            character.Z = z;
            character.Bind = -1;
        }

        account.Nation = newNation;
        // One SaveChanges commits the certificate, every character and account nation together.
        await db.SaveChangesAsync();
        return true;
    }

    private async Task RestoreCertificateAsync(UserSession session, byte[] before)
    {
        var original = Enumerable.Range(0, session.Inventory.Length).Select(_ => new ItemSlot()).ToArray();
        UserSessionBinaryState.LoadItems(original, before);
        for (var index = InventoryConstants.InventoryStart; index < original.Length; index++)
        {
            var item = original[index];
            if (item.ItemId != NationTransferItem)
                continue;
            var target = session.Inventory[index];
            if (target.ItemId == item.ItemId && target.Count == item.Count && target.Durability == item.Durability)
                continue;
            target.ItemId = item.ItemId;
            target.Count = item.Count;
            target.Durability = item.Durability;
            target.Flag = item.Flag;
            target.ExpiresAt = item.ExpiresAt;
            target.UniqueId = item.UniqueId;
            await userNotificationService.SendStackChangeAsync(session, (byte)index, item.ItemId, item.Count, item.Durability);
        }
        var coefficient = gameDataService.GetCoefficient(session.Class);
        if (coefficient != null)
            session.RecalculateStats(coefficient, gameDataService);
        await userNotificationService.SendWeightChangeAsync(session);
    }
}
