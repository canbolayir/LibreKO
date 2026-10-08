using System.Text;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface IBeautyShopPacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public class BeautyShopPacketCoordinator(
    IServiceScopeFactory scopeFactory,
    SessionManager sessions,
    IWorldPacketCoordinator world,
    ILogger<BeautyShopPacketCoordinator> logger) : IBeautyShopPacketCoordinator
{
    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessions.GetByClientId(client.Id);
        if (session == null) return;
        if (packet.RemainingBytes < 2)
        {
            await Refuse(client); return;
        }
        byte sub = packet.ReadByte(), length = packet.ReadByte();
        if (sub is not 0 and not 1 || length is 0 or > 20 || packet.RemainingBytes != length + 5)
        {
            await Refuse(client); return;
        }
        string name = Encoding.ASCII.GetString(packet.ReadBytes(length));
        byte face = packet.ReadByte();
        int hair = packet.ReadInt();
        if (!string.Equals(name, session.Name, StringComparison.OrdinalIgnoreCase)
            || session.Hp <= 0 || session.Trade.IsTrading || session.Trade.IsMerchanting || session.IsGathering)
        {
            await Refuse(client); return;
        }

        Packet result;
        try
        {
            using var scope = scopeFactory.CreateScope();
            result = await scope.ServiceProvider.GetRequiredService<IPreGameService>()
                .ChangeHairAsync(session.AccountId, sub, session.Name, face, hair);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not save beauty shop appearance for {Name}", session.Name);
            await Refuse(client); return;
        }
        await client.SendPacket(result);
        if (result.GetData()[0] != 0 || sessions.GetByClientId(client.Id) != session) return;
        // Existing spawn packets carry face, hairstyle and RGB without a zone reload.
        await world.BroadcastUserInOutAsync(session, InOutType.Out);
        await world.BroadcastUserInOutAsync(session, InOutType.In);
    }

    private static Task Refuse(IClient client) => client.SendPacket(PreGamePacketWriter.ChangeHairResult(1));
}
