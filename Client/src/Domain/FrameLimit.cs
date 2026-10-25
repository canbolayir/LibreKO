namespace LibreKO.Domain;

public static class FrameLimit
{
    private const double RefreshTolerance = 0.5;

    public const int UnfocusedFps = 10;
    public const int MinimizedFps = 60;

    public static int EngineCap(int cap, bool vsync, double refreshRate)
    {
        if (cap <= 0) return 0;
        if (vsync && refreshRate > 0 && cap >= refreshRate - RefreshTolerance) return 0;
        return cap;
    }

    public static int WindowCap(int cap, bool focused, bool minimized) =>
        minimized ? (cap <= 0 ? MinimizedFps : Math.Min(cap, MinimizedFps))
        : focused ? cap
        : UnfocusedFps;
}
