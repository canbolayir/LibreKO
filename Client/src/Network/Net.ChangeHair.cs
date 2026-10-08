using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte ChangeHairSubDefault = 1;
    public const byte ChangeHairResultOk = 0;
    public const byte ChangeHairResultFail = 1;
    public const byte ChangeHairOpenShop = 2;

    public event Action<bool, int, int>? ChangeHairResultEvent;
    public event Action? BeautyShopEvent;

    private int _changeHairReqFace;
    private int _changeHairReqHair;
    private bool _changeHairPending;

    private void HandleChangeHair(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte result = p.ReadByte();
        if (result == ChangeHairOpenShop) { BeautyShopEvent?.Invoke(); return; }
        if (!_changeHairPending || result is not ChangeHairResultOk and not ChangeHairResultFail) return;
        _changeHairPending = false;
        if (result == ChangeHairResultOk)
        {
            var me = LastEnter;
            me.Face = _changeHairReqFace; me.Hair = _changeHairReqHair;
            LastEnter = me;
        }
        ChangeHairResultEvent?.Invoke(result == ChangeHairResultOk, _changeHairReqFace, _changeHairReqHair);
    }

    public bool SendChangeHair(int hair, int face)
    {
        if (_changeHairPending || face is < 0 or > 255 || string.IsNullOrEmpty(LastEnter.Name)) return false;
        _changeHairPending = true;
        _changeHairReqFace = face;
        _changeHairReqHair = hair;

        var p = new Packet(GameOpcodes.GS_CHANGE_HAIR);
        p.WriteByte(ChangeHairSubDefault);
        p.WriteSByteString(LastEnter.Name ?? "");
        p.WriteByte((byte)face);
        p.WriteInt(hair);
        _conn.Send(p);
        return true;
    }

    private void ResetChangeHair()
    {
        if (!_changeHairPending) return;
        _changeHairPending = false;
        ChangeHairResultEvent?.Invoke(false, _changeHairReqFace, _changeHairReqHair);
    }
}
