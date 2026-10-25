using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class ItemRentalTests
{
    private const int Sword = 120110000;
    private const int Potion = 389010000;
    private const byte Moradon = 21;
    private const int StartingCoins = 1_000_000;

    private static readonly ItemData SwordData = new() { Num = Sword, Name = "Iron Sword", Kind = 21, Slot = 1, Duration = 5000, Class = 1 };
    private static readonly ItemData PotionData = new() { Num = Potion, Name = "Potion", Kind = 255, Slot = 26, Countable = 1 };

    private sealed class DirectPersister(IServiceScopeFactory scopes) : ICharacterStatePersister
    {
        public Task<bool> SaveAsync(UserSession session, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SaveAndSealAsync(UserSession session, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task SetOnlineStateAsync(int characterId, bool isOnline, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveQuestStateAsync(UserSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<StateCommit> CommitAsync(UserSession session, Func<AppDbContext, Task<bool>> change, Action undo,
            CancellationToken cancellationToken = default, Func<AppDbContext, Task<bool>>? landed = null) => Run(change);

        public Task<StateCommit> CommitTogetherAsync(UserSession session, Func<AppDbContext, Task<bool>> change, Action undo,
            CancellationToken cancellationToken = default, Func<AppDbContext, Task<bool>>? landed = null) => Run(change);

        private async Task<StateCommit> Run(Func<AppDbContext, Task<bool>> change)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await change(db))
                return StateCommit.Unchanged;
            await db.SaveChangesAsync();
            return StateCommit.Saved;
        }
    }

    private sealed class Market : IDisposable
    {
        public readonly ServiceProvider Provider;
        public readonly SessionManager Sessions = new();
        public readonly ItemRentalService Rental;
        public readonly int Npc;
        public readonly Dictionary<UserSession, List<Packet>> Sent = new();

        public Market()
        {
            var services = new ServiceCollection();
            var name = $"Rental_{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(name)
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
            Provider = services.BuildServiceProvider();
            var gameData = Substitute.For<IGameDataService>();
            gameData.GetItem(Sword).Returns(SwordData);
            gameData.GetItem(Potion).Returns(PotionData);
            var scopes = Provider.GetRequiredService<IServiceScopeFactory>();
            Rental = new ItemRentalService(Sessions, scopes, gameData, Substitute.For<IUserNotificationService>(), new DirectPersister(scopes),
                Options.Create(new GameServerSettings()), Substitute.For<ILogger<ItemRentalService>>());
            Npc = Sessions.Regions.SpawnNpc(new NpcInstance
            {
                NpcId = 14401, NpcType = NpcData.TypeRental, Name = "Helard", ZoneId = Moradon, X = 100, Z = 100, MaxHp = 100, Hp = 100,
            }).UniqueId;
        }

        public UserSession Join(int characterId, string name)
        {
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            var sent = new List<Packet>();
            client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var session = Sessions.CreateSession(client, characterId, characterId + 1000);
            session.Name = name;
            session.ZoneId = Moradon;
            session.Hp = 100;
            session.X = 100;
            session.Z = 100;
            session.Money = StartingCoins;
            Sessions.Regions.AddToRegion(session);
            Sent[session] = sent;
            return session;
        }

        public (bool Ok, string Message) Result(UserSession session)
        {
            var packet = Sent[session].Last(p => { p.ResetOffset(); return p.GetOpcode() == (byte)GameOpcodes.GS_ITEM_RENTAL && p.ReadByte() == (byte)ItemRentalSub.Result; });
            packet.ResetOffset();
            packet.ReadByte();
            return (packet.ReadByte() == 1, packet.ReadString());
        }

        public RentalListingData Listing()
        {
            using var scope = Provider.CreateScope();
            return scope.ServiceProvider.GetRequiredService<AppDbContext>().RentalListings.AsNoTracking().Single();
        }

        public void Age(Func<RentalListingData, Task> change)
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            change(db.RentalListings.Single()).GetAwaiter().GetResult();
            db.SaveChanges();
        }

        public void Dispose() => Provider.Dispose();
    }

    private static ItemSlot Bag(UserSession session, int bagSlot) => session.Inventory[InventoryConstants.InventoryStart + bagSlot];

    [Fact]
    public void OnlySingleTradableGearCanBeRentedOut()
    {
        ItemRentalRules.Rentable(SwordData, new ItemSlot { ItemId = Sword, Count = 1 }).Should().BeTrue();
        ItemRentalRules.Rentable(SwordData, new ItemSlot { ItemId = Sword, Count = 1, Flag = (byte)ItemFlag.Rented }).Should().BeFalse();
        ItemRentalRules.Rentable(SwordData, new ItemSlot { ItemId = Sword, Count = 1, ExpiresAt = 1 }).Should().BeFalse();
        ItemRentalRules.Rentable(PotionData, new ItemSlot { ItemId = Potion, Count = 1 }).Should().BeFalse();
    }

    [Fact]
    public void TheOwnerEarnsThePaymentLessTheTax() => ItemRentalRules.OwnerShare(30_000, 10).Should().Be(27_000);

    [Fact]
    public async Task AListedItemIsRentedAsAnExpiringCopyAndComesBackAfterwards()
    {
        using var market = new Market();
        var owner = market.Join(1, "Rikka");
        var renter = market.Join(2, "Zeus");
        Bag(owner, 0).ItemId = Sword;
        Bag(owner, 0).Count = 1;
        Bag(owner, 0).Durability = 4200;

        await market.Rental.ListAsync(owner, market.Npc, 0, Sword, 5, 10_000);

        market.Result(owner).Ok.Should().BeTrue();
        Bag(owner, 0).IsEmpty.Should().BeTrue();
        owner.Money.Should().Be(StartingCoins - 5 * new GameServerSettings().Rental.ListingFeePerDay);
        var listing = market.Listing();
        (listing.ItemId, listing.Durability, listing.DailyPrice).Should().Be((Sword, (short)4200, 10_000));

        await market.Rental.RentAsync(renter, market.Npc, listing.Id, 3, 10_000, listing.Version);

        market.Result(renter).Ok.Should().BeTrue();
        renter.Money.Should().Be(StartingCoins - 30_000);
        var copy = Bag(renter, 0);
        (copy.ItemId, copy.State, copy.Durability).Should().Be((Sword, ItemFlag.Rented, (short)4200));
        copy.ExpiresAt.Should().BeGreaterThan(DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds());
        var rented = market.Listing();
        rented.Earnings.Should().Be(27_000);
        rented.Version.Should().Be(listing.Version + 1);

        await market.Rental.CollectItemAsync(owner, market.Npc, listing.Id);
        market.Result(owner).Ok.Should().BeFalse();

        market.Age(row => { row.RentedUntil = DateTime.UtcNow.AddMinutes(-1); return Task.CompletedTask; });
        await market.Rental.CollectEarningsAsync(owner, market.Npc, listing.Id);
        owner.Money.Should().Be(StartingCoins - 50_000 + 27_000);
        await market.Rental.CollectItemAsync(owner, market.Npc, listing.Id);

        market.Result(owner).Ok.Should().BeTrue();
        (Bag(owner, 0).ItemId, Bag(owner, 0).Durability, Bag(owner, 0).State).Should().Be((Sword, (short)4200, ItemFlag.Unsealed));
        using var scope = market.Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().RentalListings.Should().BeEmpty();
    }

    [Fact]
    public async Task AStaleOfferOrYourOwnListingIsNotRented()
    {
        using var market = new Market();
        var owner = market.Join(1, "Rikka");
        var renter = market.Join(2, "Zeus");
        Bag(owner, 0).ItemId = Sword;
        Bag(owner, 0).Count = 1;
        await market.Rental.ListAsync(owner, market.Npc, 0, Sword, 5, 10_000);
        var listing = market.Listing();

        await market.Rental.RentAsync(owner, market.Npc, listing.Id, 1, 10_000, listing.Version);
        market.Result(owner).Ok.Should().BeFalse();
        await market.Rental.RentAsync(renter, market.Npc, listing.Id, 1, 9_000, listing.Version);
        market.Result(renter).Ok.Should().BeFalse();
        await market.Rental.RentAsync(renter, market.Npc, listing.Id, 9, 10_000, listing.Version);
        market.Result(renter).Ok.Should().BeFalse();

        renter.Money.Should().Be(StartingCoins);
        Bag(renter, 0).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task NothingHappensAwayFromTheRentalNpc()
    {
        using var market = new Market();
        var owner = market.Join(1, "Rikka");
        owner.X = 300;
        Bag(owner, 0).ItemId = Sword;
        Bag(owner, 0).Count = 1;

        await market.Rental.ListAsync(owner, market.Npc, 0, Sword, 5, 10_000);

        market.Result(owner).Should().Be((false, "Talk to an item rental NPC first."));
        Bag(owner, 0).ItemId.Should().Be(Sword);
    }

    [Fact]
    public void TheBoardFiltersSortsAndPages()
    {
        using var market = new Market();
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(1, 25).Select(id => new RentalListingData
        {
            Id = id, OwnerCharacterId = 9, OwnerName = "Seller", ItemId = Sword, DailyPrice = id * 100,
            ListedAt = now.AddMinutes(id), ListingEndsAt = now.AddDays(3),
        }).ToList();

        var first = market.Rental.BuildBoard(1, new RentalQuery(false, 0, "iron", RentalCategory.Weapon, RentalClass.Warrior, RentalSort.PriceHigh), rows, now);
        var second = market.Rental.BuildBoard(1, new RentalQuery(false, 1, "", RentalCategory.Any, RentalClass.Any, RentalSort.PriceLow), rows, now);
        var none = market.Rental.BuildBoard(1, new RentalQuery(false, 0, "", RentalCategory.Armor, RentalClass.Any, RentalSort.Newest), rows, now);

        first.Total.Should().Be(25);
        first.Offers.Should().HaveCount(ItemRentalRules.PageSize);
        first.Offers[0].Listing.DailyPrice.Should().Be(2_500);
        first.Offers[0].WholeDaysLeft.Should().Be(3);
        second.Offers.Select(offer => offer.Listing.DailyPrice).Should().Equal(2_100, 2_200, 2_300, 2_400, 2_500);
        none.Total.Should().Be(0);
    }
}
