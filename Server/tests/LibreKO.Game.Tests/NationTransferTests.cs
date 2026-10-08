using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class NationTransferTests : GameTestBase
{
    private const int TransferItem = 810096000;
    private const byte WarStatus = 1;
    private const byte WarRunning = 8;
    private const byte OpenBox = 2;
    private const byte Submit = 3;
    private const byte Accepted = 1;
    private const byte InClan = 2;
    private const byte WrongCharacter = 5;
    private const byte NoItem = 7;
    private const short ElMoradRogue = 208;
    private const short ElMoradPriest = 211;

    [Theory]
    [InlineData((byte)CharacterRace.KarusArchTuarek, (short)105, (byte)CharacterRace.ElMoradBarbarian)]
    [InlineData((byte)CharacterRace.KarusTuarek, (short)108, (byte)CharacterRace.ElMoradMale)]
    [InlineData((byte)CharacterRace.KarusWrinkleTuarek, (short)110, (byte)CharacterRace.ElMoradMale)]
    [InlineData((byte)CharacterRace.KarusPuriTuarek, (short)111, (byte)CharacterRace.ElMoradFemale)]
    [InlineData((byte)CharacterRace.KarusKurian, (short)115, (byte)CharacterRace.ElMoradPorutu)]
    [InlineData((byte)CharacterRace.ElMoradFemale, (short)206, (byte)CharacterRace.KarusArchTuarek)]
    [InlineData((byte)CharacterRace.ElMoradMale, (short)208, (byte)CharacterRace.KarusTuarek)]
    [InlineData((byte)CharacterRace.ElMoradMale, (short)210, (byte)CharacterRace.KarusWrinkleTuarek)]
    [InlineData((byte)CharacterRace.ElMoradFemale, (short)210, (byte)CharacterRace.KarusPuriTuarek)]
    [InlineData((byte)CharacterRace.ElMoradMale, (short)212, (byte)CharacterRace.KarusTuarek)]
    [InlineData((byte)CharacterRace.ElMoradFemale, (short)212, (byte)CharacterRace.KarusPuriTuarek)]
    public void TheOtherNationOffersTheMatchingBody(byte race, short classId, byte proposed) =>
        NationTransferRules.ProposedRace(race, classId).Should().Be(proposed);

    [Theory]
    [InlineData((short)108, (short)208)]
    [InlineData((short)211, (short)111)]
    public void TheClassKeepsItsJobInTheOtherNation(short classId, short newClass) =>
        NationTransferRules.NewClass(classId).Should().Be(newClass);

    private ServiceProvider Provider(short clanOfSecond = 0, Action<IServiceCollection>? configureServices = null) => CreateProvider(
        db =>
        {
            db.Accounts.Add(new Account { Login = "mover", Password = "pw", Nation = AccountNation.ElMorad, Authority = AccountAuthority.Normal });
            db.SaveChanges();
            var accountId = db.Accounts.Single(a => a.Login == "mover").Id;
            db.Characters.Add(new Character
            {
                AccountId = accountId, Slot = 0, Name = "Rover", Level = 60, Class = ElMoradRogue,
                Race = (byte)CharacterRace.ElMoradMale, Face = 2, Hair = 3, MapId = 2, X = 500, Z = 500, Hp = 100, Mp = 100,
            });
            db.Characters.Add(new Character
            {
                AccountId = accountId, Slot = 1, Name = "Healer", Level = 70, Class = ElMoradPriest,
                Race = (byte)CharacterRace.ElMoradFemale, Face = 1, Hair = 1, MapId = 2, X = 600, Z = 600, Hp = 100, Mp = 100,
                KnightsId = clanOfSecond,
            });
        },
        gameData => gameData.GetItem(TransferItem).Returns(new ItemData
        {
            Num = TransferItem, Name = "Nation Transfer Certificate", Kind = 255, Slot = 15, Duration = 1, ReqLevelMax = 100,
        }), configureServices: configureServices);

    private static async Task<(UserSession Session, List<Packet> Sent, IClient Client)> Online(ServiceProvider provider, bool carriesItem)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var character = db.Characters.Single(c => c.Name == "Rover");
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessions = provider.GetRequiredService<SessionManager>();
        var session = sessions.CreateSession(client, character.Id, character.AccountId);
        session.Name = "Rover";
        session.Class = ElMoradRogue;
        session.Race = (byte)CharacterRace.ElMoradMale;
        session.Nation = AccountNation.ElMorad;
        session.Level = 60;
        session.ZoneId = 21;
        session.X = 800;
        session.Z = 400;
        session.Hp = 100;
        session.MaxHp = 100;
        if (carriesItem)
        {
            session.Inventory[InventoryConstants.SlotMax].ItemId = TransferItem;
            session.Inventory[InventoryConstants.SlotMax].Count = 1;
            session.Inventory[InventoryConstants.SlotMax].Durability = 1;
        }
        sessions.Regions.AddToRegion(session);
        return (session, sent, client);
    }

    private static Packet Last(List<Packet> sent)
    {
        var packet = sent.Last(p => p.GetOpcode() == (byte)GameOpcodes.GS_NATION_TRANSFER);
        packet.ResetOffset();
        return packet;
    }

    [Fact]
    public async Task KaishanListsEveryCharacterWithItsBodyInTheOtherNation()
    {
        using var provider = Provider();
        var (session, sent, _) = await Online(provider, carriesItem: true);

        await provider.GetRequiredService<INationTransferService>().OpenAsync(session);

        var packet = Last(sent);
        packet.ReadByte().Should().Be(OpenBox);
        packet.ReadByte().Should().Be(Accepted);
        packet.ReadByte().Should().Be(2);
        packet.ReadShort().Should().Be(0);
        packet.ReadString().Should().Be("Rover");
        packet.ReadByte().Should().Be((byte)CharacterRace.KarusTuarek);
        packet.ReadByte().Should().Be((byte)AccountNation.Karus);
        packet.ReadShort().Should().Be(108);
        packet.ReadByte().Should().Be(2);
        packet.ReadInt().Should().Be(3);
        packet.ReadShort().Should().Be(1);
        packet.ReadString().Should().Be("Healer");
        packet.ReadByte().Should().Be((byte)CharacterRace.KarusPuriTuarek);
        packet.ReadByte().Should().Be((byte)AccountNation.Karus);
        packet.ReadShort().Should().Be(111);
    }

    [Fact]
    public async Task WithoutTheCertificateTheBoxRefuses()
    {
        using var provider = Provider();
        var (session, sent, _) = await Online(provider, carriesItem: false);

        await provider.GetRequiredService<INationTransferService>().OpenAsync(session);

        var packet = Last(sent);
        packet.ReadByte().Should().Be(OpenBox);
        packet.ReadByte().Should().Be(NoItem);
    }

    [Fact]
    public async Task ACharacterInAClanStopsTheTransfer()
    {
        using var provider = Provider(clanOfSecond: 15);
        var (session, sent, _) = await Online(provider, carriesItem: true);

        await provider.GetRequiredService<INationTransferService>().OpenAsync(session);

        var packet = Last(sent);
        packet.ReadByte().Should().Be(OpenBox);
        packet.ReadByte().Should().Be(InClan);
    }

    private static Packet SubmitPacket(byte rogueRace, byte priestRace)
    {
        var packet = new Packet(GameOpcodes.GS_NATION_TRANSFER);
        packet.WriteByte(Submit);
        packet.WriteByte(Accepted);
        packet.WriteByte(2);
        packet.WriteShort(0);
        packet.WriteString("Rover");
        packet.WriteByte(rogueRace);
        packet.WriteByte(4);
        packet.WriteInt(0x01_102030);
        packet.WriteShort(1);
        packet.WriteString("Healer");
        packet.WriteByte(priestRace);
        packet.WriteByte(5);
        packet.WriteInt(0x02_405060);
        packet.ResetOffset();
        return packet;
    }

    [Fact]
    public async Task TheWholeAccountMovesToKarus()
    {
        using var provider = Provider();
        var (session, sent, client) = await Online(provider, carriesItem: true);

        await provider.GetRequiredService<INationTransferService>()
            .HandleAsync(client, SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusTuarek));

        var reply = Last(sent);
        reply.ReadByte().Should().Be(Submit);
        reply.ReadByte().Should().Be(Accepted);
        session.Nation.Should().Be(AccountNation.Karus);
        session.Class.Should().Be(108);
        session.Race.Should().Be((byte)CharacterRace.KarusTuarek);
        session.Face.Should().Be(4);
        session.Inventory[InventoryConstants.SlotMax].IsEmpty.Should().BeTrue();

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Single(a => a.Login == "mover").Nation.Should().Be(AccountNation.Karus);
        var healer = db.Characters.Single(c => c.Name == "Healer");
        healer.Class.Should().Be(111);
        healer.Race.Should().Be((byte)CharacterRace.KarusTuarek);
        healer.Face.Should().Be(5);
        healer.Hair.Should().Be(0x02_405060);
        healer.MapId.Should().Be(21);
    }

    [Fact]
    public async Task ABodyTheNewClassCannotTakeRefusesTheWholeTransfer()
    {
        using var provider = Provider();
        var (session, sent, client) = await Online(provider, carriesItem: true);

        await provider.GetRequiredService<INationTransferService>()
            .HandleAsync(client, SubmitPacket((byte)CharacterRace.KarusArchTuarek, (byte)CharacterRace.KarusPuriTuarek));

        var reply = Last(sent);
        reply.ReadByte().Should().Be(Submit);
        reply.ReadByte().Should().Be(WrongCharacter);
        session.Nation.Should().Be(AccountNation.ElMorad);
        session.Inventory[InventoryConstants.SlotMax].IsEmpty.Should().BeFalse();

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Characters.Single(c => c.Name == "Healer").Class.Should().Be(ElMoradPriest);
    }

    [Fact]
    public async Task NoTransferDuringANationWar()
    {
        using var provider = Provider();
        var (session, sent, _) = await Online(provider, carriesItem: true);
        var battle = provider.GetRequiredService<SessionManager>().Battle;
        battle.OpenBattleZone(BattleZoneManager.BATTLEZONE_OPEN, BattleZoneManager.ZONE_BATTLE1);
        battle.KilledElmoNpc = 2;
        battle.KilledKarusNpc = 1;

        await provider.GetRequiredService<INationTransferService>().OpenAsync(session);

        var packet = Last(sent);
        packet.ReadByte().Should().Be(WarStatus);
        packet.ReadByte().Should().Be(WarRunning);
        packet.ReadByte().Should().Be(2);
        packet.ReadByte().Should().Be(1);
    }

    [Fact]
    public async Task EveryTruncatedSubmissionLeavesTheAccountAndCertificateUntouched()
    {
        using var provider = Provider();
        var (session, sent, client) = await Online(provider, carriesItem: true);
        var service = provider.GetRequiredService<INationTransferService>();
        var bytes = SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek).GetData();
        for (var length = 0; length < bytes.Length; length++)
        {
            sent.Clear();
            var partial = new Packet(GameOpcodes.GS_NATION_TRANSFER);
            partial.WriteBytes(bytes[..length]);
            await service.HandleAsync(client, partial);
            session.Nation.Should().Be(AccountNation.ElMorad, $"prefix length {length}");
            session.Inventory[InventoryConstants.SlotMax].Count.Should().Be(1);
            sent.Where(p => p.GetOpcode() == (byte)GameOpcodes.GS_NATION_TRANSFER)
                .Should().NotContain(p => p.GetData().SequenceEqual(new byte[] { Submit, Accepted }));
        }
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Single().Nation.Should().Be(AccountNation.ElMorad);
        db.Characters.Single(c => c.Name == "Healer").Class.Should().Be(ElMoradPriest);
    }

    [Theory]
    [InlineData("negative-name-length")]
    [InlineData("oversized-name")]
    [InlineData("empty-name")]
    [InlineData("negative-slot")]
    [InlineData("duplicate-slot")]
    [InlineData("trailing-data")]
    [InlineData("zero-count")]
    public async Task MalformedSubmissionsCannotSpendTheCertificate(string malformed)
    {
        using var provider = Provider();
        var (session, sent, client) = await Online(provider, carriesItem: true);
        var bytes = SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek).GetData();
        switch (malformed)
        {
            case "negative-name-length": bytes[5] = bytes[6] = 255; break;
            case "oversized-name": bytes[5] = 21; break;
            case "empty-name": bytes[5] = bytes[6] = 0; break;
            case "negative-slot": bytes[3] = bytes[4] = 255; break;
            case "duplicate-slot": bytes[18] = bytes[19] = 0; break;
            case "trailing-data": bytes = [.. bytes, 0]; break;
            case "zero-count": bytes = [Submit, Accepted, 0]; break;
        }
        var packet = new Packet(GameOpcodes.GS_NATION_TRANSFER);
        packet.WriteBytes(bytes);
        await provider.GetRequiredService<INationTransferService>().HandleAsync(client, packet);
        Last(sent).GetData().Should().Equal(Submit, WrongCharacter);
        session.Nation.Should().Be(AccountNation.ElMorad);
        session.Inventory[InventoryConstants.SlotMax].Count.Should().Be(1);
    }

    [Fact]
    public async Task AFailedCertificateConsumptionCannotChangeAnyCharacter()
    {
        var usage = Substitute.For<IMagicItemUsageService>();
        usage.CanUseItem(Arg.Any<UserSession>(), TransferItem, 1).Returns(true);
        usage.TryConsumeItemAsync(Arg.Any<UserSession>(), TransferItem, 1).Returns(false);
        using var provider = Provider(configureServices: services => services.AddSingleton(usage));
        var (session, sent, client) = await Online(provider, carriesItem: true);
        await provider.GetRequiredService<INationTransferService>().HandleAsync(client,
            SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek));
        Last(sent).GetData().Should().Equal(Submit, NoItem);
        session.Nation.Should().Be(AccountNation.ElMorad);
        session.Class.Should().Be(ElMoradRogue);
        session.Inventory[InventoryConstants.SlotMax].Count.Should().Be(1);
        provider.GetRequiredService<SessionManager>().GetByClientId(client.Id).Should().BeSameAs(session);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Single().Nation.Should().Be(AccountNation.ElMorad);
        db.Characters.Single(c => c.Name == "Healer").Class.Should().Be(ElMoradPriest);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("trade")]
    [InlineData("merchant")]
    [InlineData("merchant-preparing")]
    [InlineData("gathering")]
    public async Task IncompatibleActivitiesCannotOpenOrSubmitTransfer(string activity)
    {
        using var provider = Provider();
        var (session, sent, client) = await Online(provider, carriesItem: true);
        switch (activity)
        {
            case "dead": session.Hp = 0; break;
            case "trade": session.Trade.ExchangeUser = 999; break;
            case "merchant": session.Trade.MerchantState = MerchantMode.Selling; break;
            case "merchant-preparing": session.Trade.IsSellingMerchantPreparing = true; break;
            case "gathering": session.IsMining = true; break;
        }
        var service = provider.GetRequiredService<INationTransferService>();
        await service.OpenAsync(session);
        Last(sent).GetData().Should().Equal(OpenBox, 0);
        await service.HandleAsync(client, SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek));
        Last(sent).GetData().Should().Equal(Submit, 0);
        session.Inventory[InventoryConstants.SlotMax].Count.Should().Be(1);
        session.Nation.Should().Be(AccountNation.ElMorad);
    }

    private sealed class TransferSaveProbe(bool fail = false) : SaveChangesInterceptor
    {
        public int MigrationSaves { get; private set; }
        public int MigratedCharacters { get; private set; }
        public bool ConsumedCertificate { get; private set; }
        public Func<Task>? BeforeMigrationSave { get; set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = (AppDbContext)eventData.Context!;
            db.ChangeTracker.DetectChanges();
            if (!db.ChangeTracker.Entries<Account>().Any(entry => entry.State == EntityState.Modified
                && entry.Property(account => account.Nation).OriginalValue == AccountNation.ElMorad
                && entry.Entity.Nation == AccountNation.Karus))
                return result;
            MigrationSaves++;
            var characters = db.ChangeTracker.Entries<Character>().Where(entry => entry.State == EntityState.Modified).ToList();
            MigratedCharacters = characters.Count;
            characters.Should().OnlyContain(entry => entry.Entity.Class / 100 == (int)AccountNation.Karus
                && entry.Entity.MapId == 21 && entry.Entity.Bind == -1);
            var active = characters.Single(entry => entry.Entity.Name == "Rover").Entity;
            var slots = Enumerable.Range(0, InventoryConstants.InventoryTotal).Select(_ => new ItemSlot()).ToArray();
            UserSessionBinaryState.LoadItems(slots, active.Items);
            ConsumedCertificate = slots.All(item => item.ItemId != TransferItem);
            if (fail)
                throw new InvalidOperationException("Injected migration save failure");
            if (BeforeMigrationSave != null)
                await BeforeMigrationSave();
            return result;
        }
    }

    [Fact]
    public async Task SuccessIsSentOnlyAfterOneCompleteMigrationSaveAndLogout()
    {
        var probe = new TransferSaveProbe();
        using var provider = Provider(configureServices: services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(probe)));
        var (session, sent, client) = await Online(provider, carriesItem: true);
        session.Money = 456789;
        session.ZoneId = 2;
        session.X = 500;
        session.Z = 600;
        bool observedSuccess = false;
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var packet = call.Arg<Packet>();
            sent.Add(packet);
            if (packet.GetOpcode() == (byte)GameOpcodes.GS_NATION_TRANSFER
                && packet.GetData().SequenceEqual(new byte[] { Submit, Accepted }))
            {
                observedSuccess = true;
                probe.MigrationSaves.Should().Be(1);
                probe.MigratedCharacters.Should().Be(2);
                probe.ConsumedCertificate.Should().BeTrue();
                provider.GetRequiredService<SessionManager>().GetByClientId(client.Id).Should().BeNull();
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Accounts.Single().Nation.Should().Be(AccountNation.Karus);
                var active = db.Characters.Single(character => character.Name == "Rover");
                active.MapId.Should().Be(21, "logout cannot restore the departure map");
                active.X.Should().Be(816);
                active.Z.Should().Be(532);
                active.Money.Should().Be(456789);
                active.Race.Should().Be((byte)CharacterRace.KarusTuarek);
                active.Class.Should().Be(108);
            }
            return Task.CompletedTask;
        });
        await provider.GetRequiredService<INationTransferService>().HandleAsync(client,
            SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek));
        observedSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AFailedMigrationSaveRestoresTheExactCertificateAndKeepsTheAccountOnline()
    {
        var probe = new TransferSaveProbe(fail: true);
        using var provider = Provider(configureServices: services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(probe)));
        var (session, sent, client) = await Online(provider, carriesItem: true);
        var certificate = session.Inventory[InventoryConstants.SlotMax];
        certificate.Flag = (byte)ItemFlag.Bound;
        certificate.UniqueId = 1245;
        certificate.ExpiresAt = 123456789;
        var before = session.SerializeItems();
        await provider.GetRequiredService<INationTransferService>().HandleAsync(client,
            SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek));
        Last(sent).GetData().Should().Equal(Submit, 0);
        probe.MigrationSaves.Should().Be(1);
        session.SerializeItems().Should().Equal(before);
        session.Nation.Should().Be(AccountNation.ElMorad);
        session.Class.Should().Be(ElMoradRogue);
        session.NationTransferCommitted.Should().BeFalse();
        provider.GetRequiredService<SessionManager>().GetByClientId(client.Id).Should().BeSameAs(session);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Single().Nation.Should().Be(AccountNation.ElMorad);
        db.Characters.Single(character => character.Name == "Rover").Class.Should().Be(ElMoradRogue);
        db.Characters.Single(character => character.Name == "Healer").Class.Should().Be(ElMoradPriest);
        await provider.GetRequiredService<ICharacterStatePersister>().SaveAsync(session);
        session.CharacterPersistenceGate.CurrentCount.Should().Be(1, "a refused operation must release its save gate");
    }

    [Fact]
    public async Task AutosaveCannotWriteTheOldDestinationAcrossAnAccountMigration()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new TransferSaveProbe { BeforeMigrationSave = () => { entered.SetResult(); return release.Task; } };
        using var provider = Provider(configureServices: services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(probe)));
        var (session, _, client) = await Online(provider, carriesItem: true);
        session.ZoneId = 2; session.X = 500; session.Z = 600;
        var transfer = provider.GetRequiredService<INationTransferService>().HandleAsync(client,
            SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var autosave = provider.GetRequiredService<ICharacterStatePersister>().SaveAsync(session);
        autosave.IsCompleted.Should().BeFalse("the migration owns the character persistence gate");
        release.SetResult();
        await Task.WhenAll(transfer, autosave).WaitAsync(TimeSpan.FromSeconds(10));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var character = db.Characters.Single(c => c.Name == "Rover");
        character.MapId.Should().Be(21);
        character.X.Should().Be(816);
        character.Z.Should().Be(532);
        character.Class.Should().Be(108);
        db.Accounts.Single().Nation.Should().Be(AccountNation.Karus);
        session.CharacterPersistenceGate.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task ACurrentKingOnAnyAccountCharacterRefusesTheWholeTransfer()
    {
        var kings = Substitute.For<IKingSystemRuntimeService>();
        kings.GetKingData(AccountNation.ElMorad).Returns(new KingSystemData { KingName = "healer" });
        using var provider = Provider(configureServices: services => services.AddSingleton(kings));
        var (session, sent, client) = await Online(provider, carriesItem: true);
        var service = provider.GetRequiredService<INationTransferService>();
        await service.OpenAsync(session);
        Last(sent).GetData().Should().Equal(OpenBox, 3);
        await service.HandleAsync(client, SubmitPacket((byte)CharacterRace.KarusTuarek, (byte)CharacterRace.KarusPuriTuarek));
        Last(sent).GetData().Should().Equal(Submit, 3);
        session.Nation.Should().Be(AccountNation.ElMorad);
        session.Inventory[InventoryConstants.SlotMax].Count.Should().Be(1);
    }

    [Fact]
    public async Task AWholeKarusAccountCanMoveToElMoradWithSeparateSavedLooks()
    {
        using var provider = Provider();
        using (var setup = provider.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Accounts.Single().Nation = AccountNation.Karus;
            var rogue = db.Characters.Single(c => c.Name == "Rover"); rogue.Class = 108; rogue.Race = 2;
            var priest = db.Characters.Single(c => c.Name == "Healer"); priest.Class = 111; priest.Race = 4;
            await db.SaveChangesAsync();
        }
        var (session, sent, client) = await Online(provider, carriesItem: true);
        session.Nation = AccountNation.Karus; session.Class = 108; session.Race = 2;
        await provider.GetRequiredService<INationTransferService>().HandleAsync(client,
            SubmitPacket((byte)CharacterRace.ElMoradMale, (byte)CharacterRace.ElMoradFemale));
        Last(sent).GetData().Should().Equal(Submit, Accepted);
        session.Nation.Should().Be(AccountNation.ElMorad);
        session.Class.Should().Be(208);
        session.Inventory[InventoryConstants.SlotMax].IsEmpty.Should().BeTrue();
        using var scope = provider.CreateScope();
        var dbFinal = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbFinal.Accounts.Single().Nation.Should().Be(AccountNation.ElMorad);
        var healer = dbFinal.Characters.Single(c => c.Name == "Healer");
        healer.Class.Should().Be(211); healer.Race.Should().Be(13);
        healer.Face.Should().Be(5); healer.Hair.Should().Be(0x02_405060);
        healer.MapId.Should().Be(21);
    }
}
