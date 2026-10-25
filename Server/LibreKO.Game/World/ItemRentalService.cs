using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public enum RentalCategory : byte
{
    Any = 0,
    Weapon = 1,
    Armor = 2,
    Accessory = 3,
}

public enum RentalClass : byte
{
    Any = 0,
    Warrior = 1,
    Rogue = 2,
    Mage = 3,
    Priest = 4,
}

public enum RentalSort : byte
{
    Newest = 0,
    PriceLow = 1,
    PriceHigh = 2,
}

public sealed record RentalQuery(bool Own, int Page, string Text, RentalCategory Category, RentalClass Class, RentalSort Sort);

public static class ItemRentalRules
{
    public const int PageSize = 20;
    public const int MaxQueryLength = 80;
    private const byte LastWeaponSlot = 4;
    private const byte LastArmorSlot = 9;
    private const byte LastRentableSlot = 14;
    private const int PercentScale = 100;

    private static readonly (byte From, byte To)[] RentableKinds =
    [
        (11, 94), (110, 110), (140, 140), (181, 181), (200, 240),
    ];

    private static readonly Dictionary<RentalClass, byte[]> ClassCodes = new()
    {
        [RentalClass.Warrior] = [1, 6],
        [RentalClass.Rogue] = [2, 8],
        [RentalClass.Mage] = [3, 10],
        [RentalClass.Priest] = [4, 12],
    };

    public static bool Rentable(ItemData item, ItemSlot slot) =>
        item.Countable == 0
        && slot.Count <= 1
        && slot.IsTradable
        && !slot.Expires
        && !item.IsUntradeable
        && item.Slot <= LastRentableSlot
        && RentableKinds.Any(kind => item.Kind >= kind.From && item.Kind <= kind.To);

    public static RentalCategory CategoryOf(ItemData item) => item.Slot switch
    {
        <= LastWeaponSlot => RentalCategory.Weapon,
        <= LastArmorSlot => RentalCategory.Armor,
        _ => RentalCategory.Accessory,
    };

    public static bool ForClass(ItemData item, RentalClass rentalClass) =>
        rentalClass == RentalClass.Any || item.Class == 0 || ClassCodes[rentalClass].Contains(item.Class);

    public static long OwnerShare(long payment, int taxPercent) => payment - payment * taxPercent / PercentScale;

    public static bool Matches(ItemData item, RentalQuery query) =>
        (query.Category == RentalCategory.Any || CategoryOf(item) == query.Category)
        && ForClass(item, query.Class)
        && (query.Text.Length == 0 || item.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<RentalListingData> Sorted(IEnumerable<RentalListingData> listings, RentalSort sort) => sort switch
    {
        RentalSort.PriceLow => listings.OrderBy(listing => listing.DailyPrice).ThenByDescending(listing => listing.ListedAt),
        RentalSort.PriceHigh => listings.OrderByDescending(listing => listing.DailyPrice).ThenByDescending(listing => listing.ListedAt),
        _ => listings.OrderByDescending(listing => listing.ListedAt),
    };
}

public interface IItemRentalService
{
    Task SendBoardAsync(UserSession session, int npcId, RentalQuery query);
    Task ListAsync(UserSession session, int npcId, byte bagSlot, int itemId, int days, int dailyPrice);
    Task RentAsync(UserSession session, int npcId, int listingId, int days, int dailyPrice, int version);
    Task CollectEarningsAsync(UserSession session, int npcId, int listingId);
    Task CollectItemAsync(UserSession session, int npcId, int listingId);
}

public sealed class ItemRentalService(
    SessionManager sessionManager,
    IServiceScopeFactory scopeFactory,
    IGameDataService gameData,
    IUserNotificationService notifications,
    ICharacterStatePersister statePersister,
    IOptions<GameServerSettings> settings,
    ILogger<ItemRentalService> logger) : IItemRentalService
{
    private const string NotHere = "Talk to an item rental NPC first.";
    private const string Closed = "The item rental market is closed.";
    private const string Busy = "Finish trading, your stall or gathering first.";
    private const string Changed = "That offer has changed. Look at it again.";
    private const string NoRoom = "Your bag is full.";
    private const string NotSaved = "That could not be saved. Try again.";

    private readonly SemaphoreSlim gate = new(1, 1);

    private RentalSettings Rules => settings.Value.Rental;

    private bool AtRentalNpc(UserSession session, int npcId)
    {
        var npc = sessionManager.Regions.GetNpc(npcId);
        if (npc == null || !npc.IsAlive || npc.ZoneId != session.ZoneId || npc.NpcType != NpcData.TypeRental || session.Hp <= 0)
            return false;
        var dx = session.X - npc.X;
        var dz = session.Z - npc.Z;
        return dx * dx + dz * dz <= GameConstants.MaxNpcInteractionRangeSq;
    }

    private static bool BagIsFree(UserSession session) =>
        !session.Trade.IsTrading && !session.Trade.IsMerchanting && !session.IsGathering;

    private string? Refusal(UserSession session, int npcId) =>
        !AtRentalNpc(session, npcId) ? NotHere
        : !Rules.Enabled ? Closed
        : !BagIsFree(session) ? Busy
        : null;

    public async Task SendBoardAsync(UserSession session, int npcId, RentalQuery query)
    {
        if (!AtRentalNpc(session, npcId))
        {
            await SendResultAsync(session, false, NotHere);
            return;
        }

        var now = DateTime.UtcNow;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = query.Own
            ? await db.RentalListings.AsNoTracking()
                .Where(listing => listing.OwnerCharacterId == session.CharacterId && (!listing.Collected || listing.Earnings > 0)).ToListAsync()
            : await db.RentalListings.AsNoTracking().Where(listing => !listing.Collected && listing.ListingEndsAt > now).ToListAsync();
        await session.Client.SendPacket(ItemRentalPacketWriter.Board(BuildBoard(session.CharacterId, query, rows, now)));
    }

    public RentalBoard BuildBoard(int characterId, RentalQuery query, IEnumerable<RentalListingData> rows, DateTime now)
    {
        var shown = rows.Where(listing => gameData.GetItem(listing.ItemId) is { } item && (query.Own || ItemRentalRules.Matches(item, query)));
        var sorted = ItemRentalRules.Sorted(shown, query.Sort).ToList();
        var pages = Math.Max(1, (sorted.Count + ItemRentalRules.PageSize - 1) / ItemRentalRules.PageSize);
        var page = Math.Clamp(query.Page, 0, pages - 1);
        var offers = sorted.Skip(page * ItemRentalRules.PageSize).Take(ItemRentalRules.PageSize)
            .Select(listing => new RentalOffer(listing, listing.OwnerCharacterId == characterId, listing.WholeDaysLeft(now), listing.RentedAt(now)))
            .ToList();
        return new RentalBoard(query.Own, Rules.Enabled, page, sorted.Count, Rules.MinDays, Rules.MaxDays, Rules.ListingFeePerDay,
            Rules.TaxPercent, new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds(), offers);
    }

    public async Task ListAsync(UserSession session, int npcId, byte bagSlot, int itemId, int days, int dailyPrice)
    {
        await gate.WaitAsync();
        try
        {
            var slotIndex = InventoryConstants.InventoryStart + bagSlot;
            var fee = (long)days * Rules.ListingFeePerDay;
            var refusal = Refusal(session, npcId)
                ?? (bagSlot >= InventoryConstants.HaveMax || gameData.GetItem(itemId) is not { } item
                    || session.Inventory[slotIndex].ItemId != itemId || !ItemRentalRules.Rentable(item, session.Inventory[slotIndex])
                    ? "That item cannot be rented out."
                    : days < Rules.MinDays || days > Rules.MaxDays ? $"List it for {Rules.MinDays} to {Rules.MaxDays} days."
                    : dailyPrice < 1 ? "Set a daily price."
                    : session.Money < fee ? $"The listing fee is {fee:N0} gold."
                    : null);
            if (refusal != null)
            {
                await SendResultAsync(session, false, refusal);
                return;
            }

            var now = DateTime.UtcNow;
            var journal = new InventoryJournal(session, logger);
            var outcome = await statePersister.CommitTogetherAsync(session, async db =>
            {
                var held = await db.RentalListings.CountAsync(listing => listing.OwnerCharacterId == session.CharacterId && (!listing.Collected || listing.Earnings > 0));
                if (held >= Rules.MaxListingsPerPlayer)
                    return false;

                var listing = new RentalListingData
                {
                    Id = (await db.RentalListings.MaxAsync(row => (int?)row.Id) ?? 0) + 1,
                    OwnerCharacterId = session.CharacterId,
                    OwnerName = session.Name,
                    ItemId = itemId,
                    DailyPrice = dailyPrice,
                    ListedAt = now,
                    ListingEndsAt = now.AddDays(days),
                };
                var taken = journal.Apply(active =>
                {
                    var slot = journal.Slot(slotIndex);
                    if (slot.ItemId != itemId || active.Money < fee)
                        return false;
                    listing.Durability = slot.Durability;
                    listing.Flag = slot.Flag;
                    listing.UniqueId = slot.UniqueId;
                    slot.Clear();
                    journal.AddMoney(-fee);
                    return true;
                });
                if (!taken)
                    return false;

                db.RentalListings.Add(listing);
                return true;
            }, () => session.WithLock(_ => journal.Restore()));

            if (outcome != StateCommit.Saved)
            {
                await SendResultAsync(session, false, outcome == StateCommit.Failed ? NotSaved : $"You can list at most {Rules.MaxListingsPerPlayer} items.");
                return;
            }

            await notifications.SendStackChangeAsync(session, (byte)slotIndex, 0, 0, 0);
            if (fee > 0)
                await notifications.SendGoldLossAsync(session, (int)fee);
            await RefreshCarriedAsync(session);
            logger.LogInformation("{Name} listed item {Item} for rent, {Days} days at {Price} a day", session.Name, itemId, days, dailyPrice);
            await SendResultAsync(session, true, $"Listed for {days} days at {dailyPrice:N0} gold a day.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RentAsync(UserSession session, int npcId, int listingId, int days, int dailyPrice, int version)
    {
        await gate.WaitAsync();
        try
        {
            if (Refusal(session, npcId) is { } refusal)
            {
                await SendResultAsync(session, false, refusal);
                return;
            }

            var now = DateTime.UtcNow;
            var cost = (long)days * dailyPrice;
            string? problem = null;
            var listingName = string.Empty;
            var ownerId = 0;
            var journal = new InventoryJournal(session, logger);
            var outcome = await statePersister.CommitTogetherAsync(session, async db =>
            {
                var listing = await db.RentalListings.FindAsync(listingId);
                var item = listing == null ? null : gameData.GetItem(listing.ItemId);
                problem = listing == null || item == null || !listing.OpenAt(now) ? "That offer is no longer listed."
                    : listing.Version != version || listing.DailyPrice != dailyPrice ? Changed
                    : listing.OwnerCharacterId == session.CharacterId ? "That is your own listing."
                    : listing.RentedAt(now) ? "Someone is renting that item now."
                    : days < 1 || days > listing.WholeDaysLeft(now) ? $"It can be rented for 1 to {listing.WholeDaysLeft(now)} days."
                    : cost > ExchangePacketConstants.CoinMax || session.Money < cost ? $"Renting it costs {cost:N0} gold."
                    : null;
                if (problem != null)
                    return false;

                var copy = new ExchangeItem
                {
                    ItemId = listing!.ItemId, Count = 1, Durability = listing.Durability, Flag = (byte)ItemFlag.Rented,
                    ExpiresAt = new DateTimeOffset(now.AddDays(days), TimeSpan.Zero).ToUnixTimeSeconds(),
                };
                var given = journal.Apply(active =>
                {
                    var destination = ItemPlacement.FindForDelivery(active.Inventory, InventoryConstants.InventoryStart, InventoryConstants.HaveMax, copy);
                    if (destination < 0 || active.Money < cost)
                        return false;
                    ItemPlacement.Put(journal.Slot(destination), copy);
                    journal.AddMoney(-cost);
                    return true;
                });
                if (!given)
                {
                    problem = NoRoom;
                    return false;
                }

                listing.RenterCharacterId = session.CharacterId;
                listing.RentedUntil = now.AddDays(days);
                listing.Earnings += ItemRentalRules.OwnerShare(cost, Rules.TaxPercent);
                listing.Version++;
                listingName = item!.Name;
                ownerId = listing.OwnerCharacterId;
                return true;
            }, () => session.WithLock(_ => journal.Restore()));

            if (outcome != StateCommit.Saved)
            {
                await SendResultAsync(session, false, problem ?? NotSaved);
                return;
            }

            await notifications.SendGoldLossAsync(session, (int)cost);
            await RefreshCarriedAsync(session);
            logger.LogInformation("{Name} rented listing {Listing} for {Days} days, {Cost} gold", session.Name, listingId, days, cost);
            await SendResultAsync(session, true, $"Rented {listingName} for {days} days. The copy disappears when the rental ends.");
            if (sessionManager.GetByCharacterId(ownerId) is { } owner)
                await owner.Client.SendPacket(ChatPacketWriter.SystemNotice((byte)owner.Nation, $"Your {listingName} was rented for {days} days."));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task CollectEarningsAsync(UserSession session, int npcId, int listingId)
    {
        await gate.WaitAsync();
        try
        {
            if (Refusal(session, npcId) is { } refusal)
            {
                await SendResultAsync(session, false, refusal);
                return;
            }

            long paid = 0;
            var journal = new InventoryJournal(session, logger);
            var outcome = await statePersister.CommitTogetherAsync(session, async db =>
            {
                var listing = await db.RentalListings.FindAsync(listingId);
                if (listing == null || listing.OwnerCharacterId != session.CharacterId || listing.Earnings <= 0)
                    return false;

                paid = Math.Min(listing.Earnings, Math.Max(0L, ExchangePacketConstants.CoinMax - (long)session.Money));
                if (paid <= 0)
                    return false;

                journal.Apply(_ => journal.AddMoney(paid));
                listing.Earnings -= paid;
                listing.Version++;
                if (listing.Collected && listing.Earnings == 0)
                    db.RentalListings.Remove(listing);
                return true;
            }, () => session.WithLock(_ => journal.Restore()));

            if (outcome != StateCommit.Saved)
            {
                await SendResultAsync(session, false, outcome == StateCommit.Failed ? NotSaved : "There are no earnings to collect, or your purse is full.");
                return;
            }

            await notifications.SendGoldGainAsync(session, (int)paid);
            await SendResultAsync(session, true, $"Collected {paid:N0} gold.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task CollectItemAsync(UserSession session, int npcId, int listingId)
    {
        await gate.WaitAsync();
        try
        {
            if (Refusal(session, npcId) is { } refusal)
            {
                await SendResultAsync(session, false, refusal);
                return;
            }

            var now = DateTime.UtcNow;
            string? problem = null;
            var journal = new InventoryJournal(session, logger);
            var outcome = await statePersister.CommitTogetherAsync(session, async db =>
            {
                var listing = await db.RentalListings.FindAsync(listingId);
                problem = listing == null || listing.OwnerCharacterId != session.CharacterId || listing.Collected ? "That item is already back with you."
                    : listing.RentedAt(now) ? "It is rented out now. Collect it when the rental ends."
                    : null;
                if (problem != null)
                    return false;

                var original = new ExchangeItem
                {
                    ItemId = listing!.ItemId, Count = 1, Durability = listing.Durability, Flag = listing.Flag, UniqueId = listing.UniqueId,
                };
                var given = journal.Apply(active =>
                {
                    var destination = ItemPlacement.FindForDelivery(active.Inventory, InventoryConstants.InventoryStart, InventoryConstants.HaveMax, original);
                    if (destination < 0)
                        return false;
                    ItemPlacement.Put(journal.Slot(destination), original);
                    return true;
                });
                if (!given)
                {
                    problem = NoRoom;
                    return false;
                }

                listing.Collected = true;
                listing.Version++;
                if (listing.Earnings == 0)
                    db.RentalListings.Remove(listing);
                return true;
            }, () => session.WithLock(_ => journal.Restore()));

            if (outcome != StateCommit.Saved)
            {
                await SendResultAsync(session, false, problem ?? NotSaved);
                return;
            }

            await RefreshCarriedAsync(session);
            logger.LogInformation("{Name} took listing {Listing} back", session.Name, listingId);
            await SendResultAsync(session, true, "The item is back in your bag.");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RefreshCarriedAsync(UserSession session)
    {
        await session.Client.SendPacket(ItemMovePacketMapper.BuildArrangedResponse(session));
        session.RecalculateStatsWithBuffs(gameData);
        await notifications.SendWeightChangeAsync(session);
    }

    private static Task SendResultAsync(UserSession session, bool ok, string message) =>
        session.Client.SendPacket(ItemRentalPacketWriter.Result(ok, message));
}
