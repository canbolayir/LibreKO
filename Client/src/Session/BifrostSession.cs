namespace LibreKO.Session;

public sealed class BifrostSession
{
    private const ulong MsPerSecond = 1000;
    private const ulong DrakiLingerMs = DrakiTower.PlateLingerSeconds * MsPerSecond;

    private int drakiLimitSeconds;
    private int drakiElapsedSeconds;
    private int drakiFrozenSecondsLeft;
    private ulong drakiClockFromMs;
    private ulong drakiPlateClosesAtMs;

    public int DrakiStage { get; private set; }

    public int DrakiSubStage { get; private set; }

    public DrakiStageEnd? DrakiFinish { get; private set; }

    public bool DrakiPlateOpen { get; private set; }

    public bool DrakiPlateShown(ulong nowMs) =>
        DrakiPlateOpen && (drakiPlateClosesAtMs == 0 || nowMs < drakiPlateClosesAtMs);

    public bool DrakiPlateClosing => drakiPlateClosesAtMs != 0;

    public int DrakiSecondsLeft(ulong nowMs)
    {
        if (!DrakiPlateOpen) return 0;
        if (DrakiFinish != null) return drakiFrozenSecondsLeft;
        ulong endsAt = drakiClockFromMs + (ulong)drakiLimitSeconds * MsPerSecond;
        return endsAt > nowMs ? (int)((endsAt - nowMs + MsPerSecond - 1) / MsPerSecond) : 0;
    }

    public int DrakiElapsedSeconds(ulong nowMs)
    {
        if (DrakiFinish is { } finish) return finish.ElapsedSeconds;
        if (DrakiSubStage == 0 || nowMs <= drakiClockFromMs) return drakiElapsedSeconds;
        return drakiElapsedSeconds + (int)((nowMs - drakiClockFromMs) / MsPerSecond);
    }

    public DrakiPlateView DrakiPlate(ulong nowMs) =>
        new(DrakiStage, DrakiSubStage, DrakiSecondsLeft(nowMs), DrakiElapsedSeconds(nowMs), DrakiFinish != null, DrakiFinish?.GaveUp ?? false);

    public event Action? DrakiTimerEvent;

    public event Action<int, int>? DrakiFloorEvent;

    public event Action? DrakiClosingEvent;

    internal void ApplyDrakiTimer(int stage, int subStage, int limitSeconds, int elapsedSeconds, ulong nowMs)
    {
        DrakiStage = stage;
        DrakiSubStage = subStage;
        drakiLimitSeconds = Math.Max(0, limitSeconds);
        drakiElapsedSeconds = Math.Max(0, elapsedSeconds);
        drakiClockFromMs = nowMs;
        drakiPlateClosesAtMs = 0;
        DrakiFinish = null;
        DrakiPlateOpen = true;
        DrakiTimerEvent?.Invoke();
        if (subStage != 0) DrakiFloorEvent?.Invoke(stage, subStage);
    }

    internal void ApplyDrakiLeave(ulong nowMs)
    {
        drakiPlateClosesAtMs = nowMs + DrakiLingerMs;
        DrakiClosingEvent?.Invoke();
    }

    internal void CloseDrakiPlate()
    {
        DrakiPlateOpen = false;
        DrakiFinish = null;
        drakiPlateClosesAtMs = 0;
    }

    public event Action<DrakiEnterResult>? DrakiEnterResultEvent;

    internal void RaiseDrakiEnterResult(DrakiEnterResult result) => DrakiEnterResultEvent?.Invoke(result);

    public event Action<DrakiRankList>? DrakiRankListEvent;

    internal void RaiseDrakiRankList(DrakiRankList list) => DrakiRankListEvent?.Invoke(list);

    internal void ApplyDrakiStageEnd(DrakiStageEnd finish, ulong nowMs)
    {
        drakiFrozenSecondsLeft = DrakiSecondsLeft(nowMs);
        DrakiStage = finish.Stage;
        DrakiSubStage = finish.SubStage;
        DrakiFinish = finish;
        DrakiPlateOpen = true;
    }

    public event Action<int, TempleEventType>? BifrostTimeEvent;

    internal void RaiseBifrostTime(int arg1, TempleEventType templeEventType) => BifrostTimeEvent?.Invoke(arg1, templeEventType);

    public event Action<TempleJoinResult, int>? BifrostJoinEvent;

    internal void RaiseBifrostJoin(TempleJoinResult arg1, int arg2) => BifrostJoinEvent?.Invoke(arg1, arg2);

    public event Action<int, int, int>? TempleSignUpsEvent;

    internal void RaiseTempleSignUps(int arg1, int arg2, int arg3) => TempleSignUpsEvent?.Invoke(arg1, arg2, arg3);

    public event Action? BifrostDisbandEvent;

    internal void RaiseBifrostDisband() => BifrostDisbandEvent?.Invoke();

    public BorderWarState BorderWar { get; } = new();

    public TempleSignUpBoard SignUps { get; } = new();

    public event Action<int, int>? TempleScreenScoreEvent;

    internal void RaiseTempleScreenScore(int arg1, int arg2) => TempleScreenScoreEvent?.Invoke(arg1, arg2);

    public event Action<int>? AltarTimerEvent;

    internal void RaiseAltarTimer(int value) => AltarTimerEvent?.Invoke(value);

    public event Action<int, int, uint>? TempleEventFinishEvent;

    internal void RaiseTempleEventFinish(int arg1, int arg2, uint arg3) => TempleEventFinishEvent?.Invoke(arg1, arg2, arg3);

    public event Action<string, byte>? AltarFlagEvent;

    internal void RaiseAltarFlag(string arg1, byte arg2) => AltarFlagEvent?.Invoke(arg1, arg2);

    public event Action<byte>? FullMoonEntryEvent;

    internal void RaiseFullMoonEntry(byte value) => FullMoonEntryEvent?.Invoke(value);

    public event Action<byte>? FullMoonTowerEvent;

    internal void RaiseFullMoonTower(byte value) => FullMoonTowerEvent?.Invoke(value);

    public event Action<byte, byte>? FullMoonStageEvent;

    internal void RaiseFullMoonStage(byte arg1, byte arg2) => FullMoonStageEvent?.Invoke(arg1, arg2);

    public int[]? FullMoonHotbar { get; private set; }

    internal void KeepFullMoonHotbar(int[] slots) => FullMoonHotbar = slots;

    internal void ForgetFullMoonHotbar() => FullMoonHotbar = null;
}
