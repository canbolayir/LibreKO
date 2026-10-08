using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class BeautyAppearanceTests : GameTestBase
{
    private const int Hair = 0x035A3820;
    private static ServiceProvider Provider(Action<IServiceCollection>? configure = null) => CreateProvider(db =>
    {
        db.Accounts.Add(new Account { Login = "beauty", Password = "pw", Nation = AccountNation.ElMorad });
        db.Accounts.Add(new Account { Login = "other", Password = "pw", Nation = AccountNation.ElMorad });
        db.SaveChanges();
        int account = db.Accounts.Single(a => a.Login == "beauty").Id;
        db.Characters.Add(new Character { AccountId = account, Name = "Shopper", Slot = 0, Race = 12, Class = 206, Face = 1, Hair = 0x02221100, Hp = 100, Money = 3000 });
        db.Characters.Add(new Character { AccountId = account, Name = "Alt", Slot = 1, Race = 12, Class = 206, Face = 1, Hair = 0x02221100 });
    }, configureServices: configure);

    private static async Task<(UserSession Session, IClient Client, List<Packet> Sent)> Player(ServiceProvider provider)
    {
        int account = await GetAccountIdAsync(provider, "beauty"), id = await GetCharacterIdAsync(provider, "Shopper");
        var client = Substitute.For<IClient>(); client.Id.Returns(Guid.NewGuid());
        client.AccountId.Returns(account); client.CharacterId.Returns(id);
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(ClonePacket(p))), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = provider.GetRequiredService<SessionManager>().CreateSession(client, id, account);
        session.Name = "Shopper"; session.Race = 12; session.Class = 206; session.Level = 60;
        session.Face = 1; session.Hair = 0x02221100; session.Hp = 100; session.Money = 3000;
        session.Nation = AccountNation.ElMorad; session.ZoneId = 21; session.X = 800; session.Z = 400;
        provider.GetRequiredService<SessionManager>().Regions.AddToRegion(session);
        return (session, client, sent);
    }

    private static Packet Request(string name = "Shopper", byte sub = 1, byte face = 0, int hair = Hair)
    {
        var packet = new Packet(GameOpcodes.GS_CHANGE_HAIR);
        packet.WriteByte(sub); packet.WriteSByteString(name); packet.WriteByte(face); packet.WriteInt(hair); packet.ResetOffset(); return packet;
    }

    private static void Result(List<Packet> sent, byte expected)
    {
        var response = sent.Should().ContainSingle().Subject;
        response.GetOpcode().Should().Be((byte)GameOpcodes.GS_CHANGE_HAIR);
        response.GetData().Should().Equal(expected);
    }

    [Fact]
    public async Task InGameApplyPersistsAppearanceUpdatesSessionAndBroadcastsToPeersWithoutReloadOrCharge()
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider);
        var peerClient = Substitute.For<IClient>(); peerClient.Id.Returns(Guid.NewGuid());
        var peerPackets = new List<Packet>();
        peerClient.SendPacket(Arg.Do<Packet>(p => peerPackets.Add(ClonePacket(p))), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessions = provider.GetRequiredService<SessionManager>();
        var peer = sessions.CreateSession(peerClient, 999, 999); peer.Name = "Peer"; peer.Hp = 100;
        peer.ZoneId = session.ZoneId; peer.X = session.X; peer.Z = session.Z; sessions.Regions.AddToRegion(peer);
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request());
        Result(sent, 0); session.Face.Should().Be(0); session.Hair.Should().Be(Hair);
        session.Race.Should().Be(12); session.Class.Should().Be(206); session.Money.Should().Be(3000);
        session.ZoneId.Should().Be(21); session.X.Should().Be(800); session.Z.Should().Be(400);
        peerPackets.Should().HaveCount(2);
        peerPackets[0].GetOpcode().Should().Be((byte)GameOpcodes.GS_USER_INOUT);
        peerPackets[0].ReadByte().Should().Be(2);
        var appearance = peerPackets[1]; appearance.ReadByte().Should().Be(1);
        appearance.ReadByte(); appearance.ReadInt().Should().Be(session.CharacterId);
        appearance.ReadSByteString().Should().Be("Shopper"); appearance.ReadBytes(4);
        appearance.ReadShort().Should().Be(0); appearance.ReadByte(); appearance.ReadBytes(14);
        appearance.ReadByte(); appearance.ReadByte().Should().Be(12); appearance.ReadShort().Should().Be(206);
        appearance.ReadBytes(6); appearance.ReadByte().Should().Be(0); appearance.ReadInt().Should().Be(Hair);
        // Exercise the normal logout/map persistence path, which previously wrote the old look back.
        (await provider.GetRequiredService<ICharacterStatePersister>().SaveAsync(session)).Should().BeTrue();
        await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = db.Characters.Single(c => c.Id == session.CharacterId);
        saved.Face.Should().Be(0); saved.Hair.Should().Be(Hair); saved.Money.Should().Be(3000);
    }

    [Theory]
    [InlineData("Alt")]
    [InlineData("AnotherAccount")]
    public async Task CannotRestyleAnotherCharacterWhileInGame(string name)
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider);
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request(name));
        Result(sent, 1); session.Face.Should().Be(1); session.Hair.Should().Be(0x02221100);
        await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Characters.Should().OnlyContain(c => c.Face == 1 && c.Hair == 0x02221100);
    }

    [Fact]
    public async Task OwnershipIsCheckedByThePersistenceService()
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Characters.Single(c => c.Name == "Shopper").AccountId = db.Accounts.Single(a => a.Login == "other").Id;
            await db.SaveChangesAsync();
        }
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request());
        Result(sent, 1); session.Face.Should().Be(1); session.Hair.Should().Be(0x02221100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExistingSubcommandsAndZeroBasedAppearanceAreSupported(byte sub)
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider);
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request(sub: sub, hair: 0x123456));
        Result(sent, 0); session.Face.Should().Be(0); session.Hair.Should().Be(0x123456);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 1 })]
    [InlineData(new byte[] { 1, 0 })]
    [InlineData(new byte[] { 2, 1, 65, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 21, 65, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 1, 65, 0, 0 })]
    public async Task MalformedApplyRepliesWithFailureInsteadOfLeavingTheClientPending(byte[] body)
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider);
        var packet = new Packet(GameOpcodes.GS_CHANGE_HAIR); packet.WriteBytes(body); packet.ResetOffset();
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, packet);
        Result(sent, 1); session.Face.Should().Be(1); session.Hair.Should().Be(0x02221100);
    }

    [Fact]
    public async Task DeadCharacterCannotRestyle()
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider); session.Hp = 0;
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request());
        Result(sent, 1); session.Face.Should().Be(1); session.Hair.Should().Be(0x02221100);
    }

    [Fact]
    public async Task PersistenceFailureReturnsFailureAndDoesNotChangeTheLiveLook()
    {
        var service = Substitute.For<IPreGameService>();
        service.ChangeHairAsync(Arg.Any<int>(), Arg.Any<byte>(), Arg.Any<string>(), Arg.Any<byte>(), Arg.Any<int>()).Returns(Task.FromException<Packet>(new IOException("Test save failure")));
        using var provider = Provider(services => services.AddScoped(_ => service)); var (session, client, sent) = await Player(provider);
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request());
        Result(sent, 1); session.Face.Should().Be(1); session.Hair.Should().Be(0x02221100);
    }

    [Theory]
    [InlineData("trade")]
    [InlineData("merchant")]
    [InlineData("mining")]
    [InlineData("fishing")]
    public async Task OtherActivitiesCannotBeInterruptedByRestyling(string activity)
    {
        using var provider = Provider(); var (session, client, sent) = await Player(provider);
        switch (activity)
        {
            case "trade": session.Trade.ExchangeUser = 999; break;
            case "merchant": session.Trade.MerchantState = MerchantMode.Selling; break;
            case "mining": session.IsMining = true; break;
            case "fishing": session.IsFishing = true; break;
        }
        await provider.GetRequiredService<IPacketHandler>().HandlePacket(client, Request());
        Result(sent, 1); session.Face.Should().Be(1); session.Hair.Should().Be(0x02221100);
    }
}
