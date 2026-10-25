using System.Net;
using System.Net.Sockets;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.Protocol;

public interface IManesSurvivalPacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public sealed class ManesSurvivalPacketCoordinator(
    SessionManager sessionManager,
    EventSchedulerService eventSchedulerService,
    IManesSurvivalService manesSurvivalService,
    IOptions<GameServerSettings> settings,
    ILogger<ManesSurvivalPacketCoordinator> logger) : IManesSurvivalPacketCoordinator
{
    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 2)
            return;

        var sub = (ManesSurvivalSubOpcode)packet.ReadByte();
        var kind = packet.ReadByte();
        switch (sub)
        {
            case ManesSurvivalSubOpcode.SignUp when kind == (byte)ManesSignUpKind.Entry && packet.RemainingBytes >= sizeof(ushort):
                await HandleSignUpAsync(session, (ManesSignUpAction)packet.ReadUShort());
                break;
            case ManesSurvivalSubOpcode.State when kind == (byte)ManesStateKind.Rank:
                await session.Client.SendPacket(manesSurvivalService.BuildRanking(session));
                break;
            case ManesSurvivalSubOpcode.Pick when kind == (byte)ManesPickKind.Choice && packet.RemainingBytes >= 1:
                var action = (ManesPickAction)packet.ReadByte();
                short code = packet.RemainingBytes >= sizeof(short) ? packet.ReadShort() : (short)0;
                await manesSurvivalService.PickAsync(session, action, code);
                break;
            case ManesSurvivalSubOpcode.Pick when kind == (byte)ManesPickKind.BonusChoice && packet.RemainingBytes >= 1:
                var bonusAction = (ManesPickAction)packet.ReadByte();
                short bonusCode = packet.RemainingBytes >= sizeof(short) ? packet.ReadShort() : (short)0;
                await manesSurvivalService.BonusPickAsync(session, bonusAction, bonusCode);
                break;
            case ManesSurvivalSubOpcode.Bag when kind == (byte)ManesBagKind.Discard && packet.RemainingBytes >= 1 + sizeof(int):
                await manesSurvivalService.DiscardAsync(session, packet.ReadByte(), packet.ReadInt());
                break;
            default:
                logger.LogDebug("Unhandled Manes' Survival request {Sub}/{Kind} from {Name}", sub, kind, session.Name);
                break;
        }
    }

    private async Task HandleSignUpAsync(UserSession session, ManesSignUpAction action)
    {
        if (action == ManesSignUpAction.Cancel)
        {
            eventSchedulerService.LeaveTempleEvent(session.CharacterId);
            await session.Client.SendPacket(ManesSurvivalPacketWriter.SignUpReply(ManesSignUpResult.Cancelled));
            return;
        }

        if (action != ManesSignUpAction.Apply)
            return;

        var result = Refusal(session) ?? ResultOf(eventSchedulerService.TryJoinTempleEvent(session, TempleEvent.ManesSurvival));
        var count = eventSchedulerService.SignUpsOf(TempleEvent.ManesSurvival).Count;
        await session.Client.SendPacket(ManesSurvivalPacketWriter.SignUpReply(result, count));
    }

    private ManesSignUpResult? Refusal(UserSession session)
    {
        var signedUp = eventSchedulerService.SignUpsOf(TempleEvent.ManesSurvival);
        if (signedUp.Contains(session.CharacterId))
            return null;
        if (signedUp.Count >= ManesSurvivalRules.Capacity(settings.Value.ManesSurvival.PlayersPerArena))
            return ManesSignUpResult.Full;
        if (settings.Value.ManesSurvival.OneEntryPerAddress && !session.IsGM && AddressOf(session.Client) is { } address
            && signedUp.Select(sessionManager.GetByCharacterId).OfType<UserSession>()
                .Any(other => !other.IsGM && address.Equals(AddressOf(other.Client))))
            return ManesSignUpResult.SameAddress;
        return null;
    }

    private static ManesSignUpResult ResultOf(TempleJoinResult joined) => joined switch
    {
        TempleJoinResult.Joined => ManesSignUpResult.Applied,
        TempleJoinResult.AlreadyJoined or TempleJoinResult.OtherDungeon => ManesSignUpResult.AlreadyJoined,
        TempleJoinResult.LevelTooLow or TempleJoinResult.LevelTooHigh => ManesSignUpResult.LevelTooLow,
        _ => ManesSignUpResult.Cancelled,
    };

    private static IPAddress? AddressOf(IClient client)
    {
        try
        {
            return (client.Socket?.RemoteEndPoint as IPEndPoint)?.Address;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
