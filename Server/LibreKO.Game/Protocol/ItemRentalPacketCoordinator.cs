using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public interface IItemRentalPacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public sealed class ItemRentalPacketCoordinator(SessionManager sessionManager, IItemRentalService rental) : IItemRentalPacketCoordinator
{
    private const int BoardBytes = 4 + 1 + 2 + 2 + 1 + 1 + 1;
    private const int ListBytes = 4 + 1 + 4 + 1 + 4;
    private const int RentBytes = 4 + 4 + 1 + 4 + 4;
    private const int CollectBytes = 4 + 4;

    public Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1)
            return Task.CompletedTask;

        return (ItemRentalRequest)packet.ReadByte() switch
        {
            ItemRentalRequest.Board when packet.RemainingBytes >= BoardBytes => rental.SendBoardAsync(session, packet.ReadInt(), ReadQuery(packet)),
            ItemRentalRequest.List when packet.RemainingBytes >= ListBytes =>
                rental.ListAsync(session, packet.ReadInt(), packet.ReadByte(), packet.ReadInt(), packet.ReadByte(), packet.ReadInt()),
            ItemRentalRequest.Rent when packet.RemainingBytes >= RentBytes =>
                rental.RentAsync(session, packet.ReadInt(), packet.ReadInt(), packet.ReadByte(), packet.ReadInt(), packet.ReadInt()),
            ItemRentalRequest.CollectEarnings when packet.RemainingBytes >= CollectBytes =>
                rental.CollectEarningsAsync(session, packet.ReadInt(), packet.ReadInt()),
            ItemRentalRequest.CollectItem when packet.RemainingBytes >= CollectBytes =>
                rental.CollectItemAsync(session, packet.ReadInt(), packet.ReadInt()),
            _ => Task.CompletedTask,
        };
    }

    private static RentalQuery ReadQuery(Packet packet)
    {
        var own = packet.ReadByte() == 1;
        var page = packet.ReadUShort();
        var text = packet.ReadString().Trim();
        if (text.Length > ItemRentalRules.MaxQueryLength)
            text = text[..ItemRentalRules.MaxQueryLength];
        return new RentalQuery(own, page, text, (RentalCategory)packet.ReadByte(), (RentalClass)packet.ReadByte(), (RentalSort)packet.ReadByte());
    }
}
