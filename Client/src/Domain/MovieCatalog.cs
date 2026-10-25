using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;

namespace LibreKO.Domain;

public sealed record MovieTrack(IReadOnlyList<Vector3> Keys, float Rate);

public sealed record MovieCamera(float Fov, float Near, MovieTrack Eye, MovieTrack At, MovieTrack Up);

public sealed record MovieCut(int Camera, float FromFrame);

public sealed record MovieActor(string Model, float Appear, float Disappear, Vector3 Position, Quaternion Rotation, Vector3 Scale);

public sealed record MovieFade(float Start, float Cover, float Clear, float Hold, string Color);

public sealed record MovieDef(
    int Id, string? Scene, float FrameStart, float FrameEnd, float EndLead, bool NoSkip, MovieFade Transition, bool WhiteTransition,
    IReadOnlyList<MovieCamera> Cameras, IReadOnlyList<MovieCut> Schedule, IReadOnlyList<MovieActor> Actors, IReadOnlyList<MovieFade> Fades);

public static class MovieCatalog
{
    public const float FramesPerSecond = 30f;
    private const string Black = "#000000";

    public static Dictionary<int, MovieDef> Parse(string json)
    {
        var movies = new Dictionary<int, MovieDef>();
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var fade = e.GetProperty("fade");
            var transition = new MovieFade(0f, Number(fade, "cover"), Number(fade, "clear"), Number(fade, "hold"), Black);
            var cameras = new List<MovieCamera>();
            if (e.TryGetProperty("cameras", out var cams))
                foreach (var c in cams.EnumerateArray())
                    cameras.Add(new MovieCamera(Number(c, "fov"), Number(c, "near"),
                        Track(c, "eye", "eyeRate"), Track(c, "at", "atRate"), Track(c, "up", "upRate")));
            var schedule = new List<MovieCut>();
            if (e.TryGetProperty("schedule", out var cuts) && cuts.ValueKind == JsonValueKind.Array)
                foreach (var cut in cuts.EnumerateArray())
                    schedule.Add(new MovieCut(cut.GetProperty("camera").GetInt32(), Number(cut, "fromFrame")));
            var actors = new List<MovieActor>();
            if (e.TryGetProperty("actors", out var list))
                foreach (var a in list.EnumerateArray())
                    actors.Add(new MovieActor(a.GetProperty("model").GetString() ?? "", Number(a, "appear"), Number(a, "disappear"),
                        Vec3(a.GetProperty("position")), Quat(a.GetProperty("rotation")), Vec3(a.GetProperty("scale"))));
            var fades = new List<MovieFade>();
            if (e.TryGetProperty("fades", out var sceneFades) && sceneFades.ValueKind == JsonValueKind.Array)
                foreach (var f in sceneFades.EnumerateArray())
                    fades.Add(new MovieFade(Number(f, "start"), Number(f, "cover"), Number(f, "clear"), Number(f, "hold"),
                        f.GetProperty("color").GetString() ?? Black));
            string? scene = e.TryGetProperty("scene", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            movies[e.GetProperty("id").GetInt32()] = new MovieDef(e.GetProperty("id").GetInt32(), scene,
                e.TryGetProperty("frameStart", out var start) ? start.GetSingle() : 0f,
                e.TryGetProperty("frameEnd", out var end) ? end.GetSingle() : 0f,
                Number(e, "endLead"), e.GetProperty("noSkip").GetBoolean(), transition, fade.GetProperty("white").GetBoolean(),
                cameras, schedule, actors, fades);
        }
        return movies;
    }

    private static float Number(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetSingle() : 0f;

    private static Vector3 Vec3(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

    private static Quaternion Quat(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle(), e[3].GetSingle());

    private static MovieTrack Track(JsonElement e, string keys, string rate)
    {
        var list = new List<Vector3>();
        foreach (var key in e.GetProperty(keys).EnumerateArray()) list.Add(Vec3(key));
        return new MovieTrack(list, Number(e, rate) is > 0f and var r ? r : FramesPerSecond);
    }
}

public static class MoviePlayback
{
    public static Vector3 Sample(MovieTrack track, float frame)
    {
        if (track.Keys.Count == 0) return Vector3.Zero;
        float index = Math.Max(0f, frame) * track.Rate / MovieCatalog.FramesPerSecond;
        int first = (int)MathF.Floor(index);
        if (first >= track.Keys.Count - 1) return track.Keys[^1];
        return Vector3.Lerp(track.Keys[first], track.Keys[first + 1], index - first);
    }

    public static int CameraAt(MovieDef movie, float frame)
    {
        if (movie.Schedule.Count == 0) return movie.Cameras.Count > 0 ? 0 : -1;
        int camera = movie.Schedule[0].Camera;
        foreach (var cut in movie.Schedule)
            if (frame >= cut.FromFrame) camera = cut.Camera;
        return camera >= 0 && camera < movie.Cameras.Count ? camera : -1;
    }

    public static float FadeAlpha(MovieFade fade, float frame)
    {
        float t = frame - fade.Start;
        if (t < 0f) return 0f;
        if (t < fade.Cover) return fade.Cover > 0f ? t / fade.Cover : 1f;
        t -= fade.Cover;
        if (t < fade.Hold) return 1f;
        t -= fade.Hold;
        return t < fade.Clear ? 1f - t / fade.Clear : 0f;
    }

    public static bool Ending(MovieDef movie, float frame) => movie.FrameEnd - frame <= movie.EndLead || frame >= movie.FrameEnd;

    public static bool Shows(MovieActor actor, float frame) =>
        (actor.Appear < 0f || frame >= actor.Appear) && (actor.Disappear < 0f || frame < actor.Disappear);

    public static float ActorSeconds(MovieActor actor, float frame) =>
        Math.Max(0f, actor.Appear < 0f ? frame : frame - actor.Appear) / MovieCatalog.FramesPerSecond;
}
