using System;
using System.Text;
using Godot;

namespace LibreKO;

public static class Diag
{
    private const int MaxLogBytes = 1 << 20;
    private const int TraceRing = 64;
    private const int PacketRing = 32;
    private const int NoSub = -1;
    private const double SilentLoopSeconds = 8;
    private const int WatchPollMs = 1000;

    private static readonly object Gate = new();
    private static readonly string[] trace = new string[TraceRing];
    private static readonly (long UtcTicks, byte Opcode, short Sub, int Length)[] packets = new (long, byte, short, int)[PacketRing];
    private static int traceAt;
    private static int packetAt;
    private static long lastBeat;
    private static volatile bool loopPaused;
    private static bool stallReported;
    private static string path = "";
    private static bool installed;

    public static string Phase { get; set; } = "startup";
    public static bool SlowLog { get; set; }

    public static System.Diagnostics.Stopwatch? Watch() =>
        SlowLog ? System.Diagnostics.Stopwatch.StartNew() : null;

    public static void Slow(string what, System.Diagnostics.Stopwatch? watch, double thresholdMs = 4)
    {
        if (watch == null) return;
        double ms = watch.Elapsed.TotalMilliseconds;
        if (ms >= thresholdMs) GD.Print($"[slow] {what} {ms:0.0}ms");
    }

    public const double HitchMs = 40;

    public static void Hitch(string what, long startedTicks)
    {
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startedTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if (ms >= HitchMs) GD.Print($"[hitch] {what} {ms:0} ms");
    }

    public static void Install()
    {
        lock (Gate)
        {
            if (installed) return;
            installed = true;
            path = System.IO.Path.Combine(OS.GetUserDataDir(), "faults.log");
        }

        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            Report("appdomain", a.ExceptionObject as Exception, a.IsTerminating ? "TERMINATING" : null);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            Report("task", a.Exception);
            a.SetObserved();
        };
        if (Engine.GetMainLoop() is SceneTree tree) StartWatchdog(tree);
    }

    private static void StartWatchdog(SceneTree tree)
    {
        Beat();
        tree.ProcessFrame += Beat;
        new System.Threading.Thread(WatchLoop) { IsBackground = true, Name = "loop-watchdog" }.Start();
    }

    private static void Beat() => System.Threading.Volatile.Write(ref lastBeat, System.Diagnostics.Stopwatch.GetTimestamp());

    public static void LoopPaused(bool paused)
    {
        loopPaused = paused;
        Beat();
    }

    private static void WatchLoop()
    {
        while (true)
        {
            System.Threading.Thread.Sleep(WatchPollMs);
            if (loopPaused || System.Diagnostics.Debugger.IsAttached) continue;
            double silent = (System.Diagnostics.Stopwatch.GetTimestamp() - System.Threading.Volatile.Read(ref lastBeat))
                            / (double)System.Diagnostics.Stopwatch.Frequency;
            if (silent < SilentLoopSeconds)
            {
                stallReported = false;
                continue;
            }
            if (stallReported) continue;
            stallReported = true;
            Report("watchdog", null, $"main loop silent for {silent:0} s");
        }
    }

    public static void Received(byte opcode, int sub, int length)
    {
        lock (Gate)
        {
            packets[packetAt] = (DateTime.UtcNow.Ticks, opcode, (short)sub, length);
            packetAt = (packetAt + 1) % PacketRing;
        }
    }

    public static void Received(byte opcode, int length) => Received(opcode, NoSub, length);

    internal static List<string> RecentPackets()
    {
        var lines = new List<string>();
        lock (Gate)
        {
            for (int i = 0; i < PacketRing; i++)
            {
                var (ticks, opcode, sub, length) = packets[(packetAt + i) % PacketRing];
                if (ticks == 0) continue;
                string at = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss.fff");
                lines.Add($"{at} {PacketLine(opcode, sub, length)}");
            }
        }
        return lines;
    }

    internal static string PacketLine(byte opcode, int sub, int length)
    {
        string name = Enum.IsDefined(typeof(LibreKO.Network.GameOpcodes), opcode)
            ? ((LibreKO.Network.GameOpcodes)opcode).ToString()
            : "?";
        return sub == NoSub
            ? $"{name} (0x{opcode:X2}), {length} bytes"
            : $"{name} (0x{opcode:X2}) sub {sub}, {length} bytes";
    }

    public static void Step(string what)
    {
        lock (Gate)
        {
            trace[traceAt] = $"{DateTime.Now:HH:mm:ss.fff} {what}";
            traceAt = (traceAt + 1) % TraceRing;
        }
    }

    public static async System.Threading.Tasks.Task Guard(
        string where, Func<System.Threading.Tasks.Task> body)
    {
        try { await body(); }
        catch (Exception e) { Report(where, e); }
    }

    public static void Report(string where, Exception? e, string? note = null)
    {
        var recent = RecentPackets();
        string text;
        lock (Gate)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ")
                .Append(where)
                .Append(" phase=").Append(Phase)
                .Append(" thread=").Append(System.Environment.CurrentManagedThreadId);
            if (note != null) sb.Append(' ').Append(note);
            sb.AppendLine();
            for (var x = e; x != null; x = x.InnerException)
            {
                sb.Append("  ").Append(x.GetType().FullName).Append(": ").AppendLine(x.Message);
                if (x.StackTrace != null) sb.AppendLine(x.StackTrace);
            }
            for (int i = 0; i < TraceRing; i++)
            {
                string? line = trace[(traceAt + i) % TraceRing];
                if (line != null) sb.Append("  · ").AppendLine(line);
            }
            foreach (string line in recent) sb.Append("  ‹ ").AppendLine(line);
            text = sb.ToString();
        }
        Append(text);
        try { GD.PushError(text); } catch { }
    }

    private static void Append(string text)
    {
        if (path.Length == 0) return;
        try
        {
            var info = new System.IO.FileInfo(path);
            if (info.Exists && info.Length > MaxLogBytes) info.Delete();
            System.IO.File.AppendAllText(path, text + System.Environment.NewLine);
        }
        catch { }
    }
}
