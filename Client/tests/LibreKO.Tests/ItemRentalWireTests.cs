using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class ItemRentalWireTests
{
    private const int Sword = 120110000;

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

    [Fact]
    public void ABoardArrivesWithItsTermsAndOffers()
    {
        var (dispatcher, session) = Readers();
        RentalBoard? shown = null;
        session.ItemRental.BoardEvent += board => shown = board;

        var p = new Packet(GameOpcodes.GS_ITEM_RENTAL);
        p.WriteByte((byte)ItemRentalSub.Board);
        p.WriteByte(0);
        p.WriteByte(1);
        p.WriteUShort(0);
        p.WriteUShort(21);
        p.WriteByte(3);
        p.WriteByte(7);
        p.WriteInt(10_000);
        p.WriteByte(10);
        p.WriteLong(1_793_000_000);
        p.WriteByte(1);
        p.WriteInt(5);
        p.WriteInt(Sword);
        p.WriteShort(4200);
        p.WriteSByteString("Rikka");
        p.WriteInt(25_000);
        p.WriteLong(1_793_259_200);
        p.WriteLong(0);
        p.WriteLong(0);
        p.WriteInt(2);
        p.WriteByte(3);
        p.WriteByte(0);
        p.WriteByte(0);
        p.WriteByte(1);
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.NotNull(shown);
        Assert.Equal((false, true, 21, 2, 3, 7, 10_000, 10), (shown.Own, shown.Enabled, shown.Total, shown.Pages, shown.MinDays, shown.MaxDays, shown.FeePerDay, shown.TaxPercent));
        var offer = Assert.Single(shown.Offers);
        Assert.Equal(new RentalOffer(5, Sword, 4200, "Rikka", 25_000, 1_793_259_200, 0, 0, 2, 3, false, false, true), offer);
        Assert.False(ItemRentalRules.CanRent(shown, offer));
        Assert.True(ItemRentalRules.CanCollectItem(offer));
    }

    [Fact]
    public void TheOpenAndResultRepliesReachTheSession()
    {
        var (dispatcher, session) = Readers();
        int opened = 0;
        (bool, string)? result = null;
        session.ItemRental.OpenEvent += npc => opened = npc;
        session.ItemRental.ResultEvent += (ok, text) => result = (ok, text);

        var open = new Packet(GameOpcodes.GS_ITEM_RENTAL);
        open.WriteByte((byte)ItemRentalSub.Open);
        open.WriteInt(10_001);
        open.ResetOffset();
        dispatcher.Dispatch(open);
        var reply = new Packet(GameOpcodes.GS_ITEM_RENTAL);
        reply.WriteByte((byte)ItemRentalSub.Result);
        reply.WriteByte(1);
        reply.WriteString("Collected 27,000 gold.");
        reply.ResetOffset();
        dispatcher.Dispatch(reply);

        Assert.Equal(10_001, opened);
        Assert.Equal((true, "Collected 27,000 gold."), result);
    }

    [Fact]
    public void ARentRequestEchoesThePriceAndVersion()
    {
        var sink = new Recorder();
        var offer = new RentalOffer(5, Sword, 4200, "Rikka", 25_000, 0, 0, 0, 2, 3, false, false, false);

        new ItemRentalRequests(sink).Rent(10_001, offer, 2);

        var p = Assert.Single(sink.Sent);
        p.ResetOffset();
        Assert.Equal((byte)ItemRentalRequest.Rent, p.ReadByte());
        Assert.Equal((10_001, 5, (byte)2, 25_000, 2), (p.ReadInt(), p.ReadInt(), p.ReadByte(), p.ReadInt(), p.ReadInt()));
    }

    [Fact]
    public void OnlySingleTradableGearCanBeListed()
    {
        var sword = new ItemData.Item { Id = Sword, Kind = 21, Slot = 1 };
        var potion = new ItemData.Item { Id = 389010000, Kind = 255, Slot = 26, Countable = 1 };

        Assert.True(ItemRentalRules.Rentable(sword, new ItemSlot { ItemId = Sword, Count = 1 }));
        Assert.False(ItemRentalRules.Rentable(sword, new ItemSlot { ItemId = Sword, Count = 1, Flag = (byte)ItemFlag.Rented }));
        Assert.False(ItemRentalRules.Rentable(sword, new ItemSlot { ItemId = Sword, Count = 1, ExpiresAt = 5 }));
        Assert.False(ItemRentalRules.Rentable(potion, new ItemSlot { ItemId = 389010000, Count = 1 }));
    }
}
