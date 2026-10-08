using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte CapeOpBuy = 0, CapeOpTicket = 1;

    public const short CapeChanged = 1;

    public event Action<bool, int, int, int, int, int>? CapeResultEvent;
    public event Action? CapeResetEvent;
    private bool _capePending;

    private void HandleCape(Packet p)
    {
        if (!_capePending || p.RemainingBytes < 2) return;
        int result = p.ReadShort();
        if (result != CapeChanged)
        {
            if (p.RemainingBytes != 0 || result is < -10 or > -1) return;
            _capePending = false;
            CapeResultEvent?.Invoke(false, result, 0, 0, 0, 0);
            return;
        }

        if (p.RemainingBytes != 10) return;
        int clanId = p.ReadShort();
        p.ReadShort();
        int capeId = p.ReadShort();
        int colour = p.ReadInt();
        if (!MyClan.InClan || clanId != MyClan.ClanId || capeId < 0 || (colour & unchecked((int)0xFF000000)) != 0) return;
        _capePending = false;
        var me = LastEnter;
        me.CapeId = capeId; me.CapeR = colour & 0xFF; me.CapeG = (colour >> 8) & 0xFF; me.CapeB = (colour >> 16) & 0xFF;
        LastEnter = me;
        CapeResultEvent?.Invoke(
            true, clanId, capeId, colour & 0xFF, (colour >> 8) & 0xFF, (colour >> 16) & 0xFF);
    }

    public bool SendCapeBuy(byte op, int capeId, byte r, byte g, byte b)
    {
        if (_capePending || op is not CapeOpBuy and not CapeOpTicket || capeId is < -1 or > short.MaxValue) return false;
        _capePending = true;
        var p = new Packet(GameOpcodes.GS_CAPE);
        p.WriteByte(op);
        p.WriteShort((short)capeId);
        p.WriteInt(r | (g << 8) | (b << 16));
        _conn.Send(p);
        return true;
    }

    private void ResetCape()
    {
        _capePending = false;
        CapeResetEvent?.Invoke();
    }
}
