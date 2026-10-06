using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LibreKO;

/// <summary>Uncapped Moradon benchmark: 15s warmup + 20s measurement after world loading.</summary>
public partial class RendererBenchmark : Node
{
    private readonly Stopwatch clock = new();
    private readonly List<double> frames = new();
    private double previous;
    private bool started;
    public override void _Ready()
    {
        SavedAccount.Load();
        if (!SavedAccount.Any) { Fail("No saved account available"); return; }
        LoginNet.I.ErrorEvent += Fail;
        Net.I.ErrorEvent += Fail;
        LoginNet.I.VersionEvent += _ => LoginNet.I.Login(SavedAccount.Name, SavedAccount.Password);
        LoginNet.I.LoginResultEvent += (ok, result) => {
            if (!ok) { Fail($"Login failed ({result})"); return; }
            LoginNet.I.RequestServerList();
        };
        LoginNet.I.ServerListEvent += servers => {
            var server = servers.FirstOrDefault();
            if (server == null) { Fail("No game server"); return; }
            LoginNet.I.Disconnect(expected: true);
            Net.I.BeginGameLogin(Config.GameHostFor(server.Address), server.Port, SavedAccount.Name, SavedAccount.Password);
        };
        Net.I.LoginResultEvent += (ok, nation) => {
            if (!ok) { Fail("Game login failed"); return; }
            Net.I.RequestCharList();
        };
        Net.I.CharListEvent += characters => {
            var character = characters.FirstOrDefault(c => !c.Empty);
            if (character == null) { Fail("No character available"); return; }
            Net.I.SelectChar(character.Name);
        };
        Net.I.EnterWorldEvent += _ => GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/World.tscn");
        LoginNet.I.ConnectToLoginServer();
    }
    private void Fail(string message)
    {
        GD.PushError("[bench] " + message);
        GetTree().Quit(1);
    }
    public override void _Process(double delta)
    {
        if (!started)
        {
            if (GetTree().CurrentScene is not World { BenchmarkReady: true } world) return;
            world.SetProcessUnhandledInput(false);
            world.SetProcessInput(false);
            clock.Start();
            started = true;
            GD.Print($"[bench] world ready, zone={Net.I.LastEnter.Zone}; warming up for 15 seconds");
            return;
        }
        double now = clock.Elapsed.TotalSeconds;
        if (now >= 15 && previous >= 15) frames.Add((now - previous) * 1000);
        previous = now;
        if (now < 35 || frames.Count == 0) return;
        frames.Sort();
        int slowCount = Math.Max(1, (int)Math.Ceiling(frames.Count * 0.01));
        var result = new {
            renderer = RenderingServer.GetCurrentRenderingMethod(),
            driver = RenderingServer.GetCurrentRenderingDriverName(),
            gpu = RenderingServer.GetVideoAdapterName(),
            zone = Net.I.LastEnter.Zone,
            positionX = Net.I.LastEnter.X, positionY = Net.I.LastEnter.Y, positionZ = Net.I.LastEnter.Z,
            width = GetViewport().GetVisibleRect().Size.X,
            height = GetViewport().GetVisibleRect().Size.Y,
            refreshHz = DisplayServer.ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen()),
            frames = frames.Count, sampleSeconds = frames.Sum() / 1000,
            averageFps = 1000 / frames.Average(),
            onePercentLowFps = 1000 / frames.TakeLast(slowCount).Average(),
            medianMs = frames[frames.Count / 2],
            p95Ms = frames[(int)((frames.Count - 1) * 0.95)],
            p99Ms = frames[(int)((frames.Count - 1) * 0.99)],
        };
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        string output = OS.GetExecutablePath().GetBaseDir();
        File.WriteAllText(Path.Combine(output, "benchmark-result.json"), json);
        GD.Print("[bench] RESULT " + json);
        Net.I.Disconnect(expected: true);
        SetProcess(false);
        _ = Shutdown.Begin(this, 100);
    }
}
