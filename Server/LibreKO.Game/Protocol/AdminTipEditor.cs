using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface IAdminTipEditor
{
    bool Handles(byte sub);
    Task HandleAsync(UserSession session, byte sub, Packet packet);
}

public sealed class AdminTipEditor(
    IGameplayTipStore store,
    IGameplayTipService tips,
    ILogger<AdminTipEditor> logger) : IAdminTipEditor
{
    public const byte ReqTips = 40;
    public const byte ReqTipSave = 41;
    public const byte ReqTipDelete = 42;
    public const byte ReqTipSettings = 43;
    public const byte AckTips = 0x1E;

    public bool Handles(byte sub) => sub is >= ReqTips and <= ReqTipSettings;

    public Task HandleAsync(UserSession session, byte sub, Packet packet) => sub switch
    {
        ReqTips => SendTipsAsync(session),
        ReqTipSave when packet.RemainingBytes >= 4 + 1 + 1 + 2 => HandleSaveAsync(session, packet),
        ReqTipDelete when packet.RemainingBytes >= 4 => HandleDeleteAsync(session, packet.ReadInt()),
        ReqTipSettings when packet.RemainingBytes >= 1 + 2 => HandleSettingsAsync(session, packet),
        _ => Task.CompletedTask,
    };

    private async Task HandleSaveAsync(UserSession session, Packet packet)
    {
        int id = packet.ReadInt();
        byte language = packet.ReadByte();
        bool enabled = packet.ReadByte() == AdminPanelPacketWriter.Granted;
        string text = packet.ReadString().Trim();
        var problem = text.Length == 0 ? "Write the tip first."
            : text.Length > GameplayTipData.MaxTextLength ? $"A tip is at most {GameplayTipData.MaxTextLength} letters."
            : !Enum.IsDefined(typeof(GameLanguage), language) ? $"No language {language}."
            : id > 0 && tips.Tips.All(tip => tip.Id != id) ? $"No tip {id}."
            : null;
        if (problem != null)
        {
            await SendResultAsync(session, false, problem);
            return;
        }

        int saved = await store.SaveTipAsync(id, (GameLanguage)language, text, enabled);
        await tips.ReloadAsync();
        logger.LogInformation("GM {Name} saved gameplay tip {Id}", session.Name, saved);
        await SendResultAsync(session, true, $"Tip {saved} saved.");
        await SendTipsAsync(session);
    }

    private async Task HandleDeleteAsync(UserSession session, int id)
    {
        if (!await store.DeleteTipAsync(id))
        {
            await SendResultAsync(session, false, $"No tip {id}.");
            return;
        }

        await tips.ReloadAsync();
        logger.LogInformation("GM {Name} deleted gameplay tip {Id}", session.Name, id);
        await SendResultAsync(session, true, $"Tip {id} deleted.");
        await SendTipsAsync(session);
    }

    private async Task HandleSettingsAsync(UserSession session, Packet packet)
    {
        bool enabled = packet.ReadByte() == AdminPanelPacketWriter.Granted;
        int interval = (ushort)packet.ReadShort();
        if (interval is < 1 or > GameplayTipSettingsData.MaxIntervalMinutes)
        {
            await SendResultAsync(session, false, $"Tips come every 1 to {GameplayTipSettingsData.MaxIntervalMinutes} minutes.");
            return;
        }

        await store.SaveSettingsAsync(enabled, interval);
        await tips.ReloadAsync();
        logger.LogInformation("GM {Name} set gameplay tips {State} every {Minutes} min", session.Name, enabled ? "on" : "off", interval);
        await SendResultAsync(session, true, enabled ? $"Tips every {interval} minutes." : "Tips are off.");
        await SendTipsAsync(session);
    }

    private Task SendTipsAsync(UserSession session)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteByte(AckTips);
        packet.WriteByte(Flag(tips.Settings.Enabled));
        packet.WriteShort((short)tips.Settings.IntervalMinutes);
        var shown = tips.Tips;
        packet.WriteShort((short)shown.Count);
        foreach (var tip in shown)
        {
            packet.WriteInt(tip.Id);
            packet.WriteByte((byte)tip.Language);
            packet.WriteByte(Flag(tip.Enabled));
            packet.WriteString(tip.Text);
        }

        return session.Client.SendPacket(packet);
    }

    private static byte Flag(bool on) => on ? AdminPanelPacketWriter.Granted : AdminPanelPacketWriter.Denied;

    private static Task SendResultAsync(UserSession session, bool ok, string message) =>
        session.Client.SendPacket(AdminPanelPacketWriter.Result(ok, message));
}
