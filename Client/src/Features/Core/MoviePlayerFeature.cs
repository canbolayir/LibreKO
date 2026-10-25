using Godot;
using GodotQuaternion = Godot.Quaternion;
using MovieVector = System.Numerics.Vector3;

namespace LibreKO.Features.Core;

internal sealed class MoviePlayerFeature : IFeature, ITickFeature, IEscapable
{
    private const string CatalogPath = "res://assets/movies/movies.json";
    private const string ActorPath = "res://assets/movies/actors/";
    private const float MinFar = 512f;

    private enum Phase { Idle, Opening, Showing, Closing }

    private readonly IWorldContext context;
    private readonly MovieSession session;
    private readonly Dictionary<string, PackedScene?> actorScenes = new();
    private readonly List<(MovieActor Actor, Node3D Node, AnimationPlayer? Anim, string? Clip)> actors = new();
    private readonly List<CanvasLayer> hiddenLayers = new();
    private Dictionary<int, MovieDef>? catalog;
    private MovieDef? movie;
    private Phase phase;
    private float transitionFrames;
    private float frame;
    private bool sceneUp;
    private CanvasLayer? overlay;
    private ColorRect cover = null!;
    private ColorRect sceneFade = null!;
    private Control shield = null!;
    private Camera3D? movieCamera;
    private Camera3D? previousCamera;
    private Node3D? actorRoot;
    private string savedBgm = "";

    public MoviePlayerFeature(IWorldContext context, MovieSession session)
    {
        this.context = context;
        this.session = session;
    }

    internal bool Playing => movie != null;

    public void Build()
    {
        session.MovieEvent += Play;
        overlay = new CanvasLayer { Layer = UiLayers.Movie, Visible = false };
        context.Root.AddChild(overlay);
        sceneFade = FullScreen(new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore });
        cover = FullScreen(new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore });
        shield = FullScreen(new Control { MouseFilter = Control.MouseFilterEnum.Stop });
    }

    private T FullScreen<T>(T control) where T : Control
    {
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay!.AddChild(control);
        return control;
    }

    public void Dispose()
    {
        session.MovieEvent -= Play;
        if (sceneUp) TearDown();
    }

    internal void Play(int movieId)
    {
        catalog ??= LoadCatalog();
        if (movie != null || !catalog.TryGetValue(movieId, out var found)) return;
        movie = found;
        phase = Phase.Opening;
        transitionFrames = 0f;
        frame = found.FrameStart;
        cover.Color = found.WhiteTransition ? new Color(1f, 1f, 1f, 0f) : new Color(0f, 0f, 0f, 0f);
        sceneFade.Color = Colors.Transparent;
        overlay!.Visible = true;
        context.Movement.CancelAutoRun();
        context.Movement.StopMoving();
    }

    private static Dictionary<int, MovieDef> LoadCatalog()
    {
        using var file = Godot.FileAccess.Open(CatalogPath, Godot.FileAccess.ModeFlags.Read);
        return file == null ? new Dictionary<int, MovieDef>() : MovieCatalog.Parse(file.GetAsText());
    }

    bool IEscapable.CloseOnEscape()
    {
        if (movie is not { } shown) return false;
        if (!shown.NoSkip && sceneUp && phase != Phase.Closing) frame = shown.FrameEnd;
        return true;
    }

    public void Tick(double delta, double nowSec)
    {
        if (movie is not { } shown) return;
        float step = (float)delta * MovieCatalog.FramesPerSecond;
        if (sceneUp) frame = Mathf.Min(frame + step, shown.FrameEnd);

        switch (phase)
        {
            case Phase.Opening:
                transitionFrames += step;
                if (!sceneUp && transitionFrames >= shown.Transition.Cover && shown.Scene != null) SetUp(shown);
                if (TransitionDone(shown)) phase = shown.Scene != null ? Phase.Showing : Phase.Idle;
                break;
            case Phase.Showing:
                if (MoviePlayback.Ending(shown, frame))
                {
                    phase = Phase.Closing;
                    transitionFrames = 0f;
                }
                break;
            case Phase.Closing:
                transitionFrames += step;
                if (sceneUp && transitionFrames >= shown.Transition.Cover) TearDown();
                if (TransitionDone(shown)) phase = Phase.Idle;
                break;
        }

        var tint = cover.Color;
        tint.A = phase is Phase.Opening or Phase.Closing ? MoviePlayback.FadeAlpha(shown.Transition, transitionFrames) : 0f;
        cover.Color = tint;
        if (sceneUp) PoseScene(shown);
        if (phase == Phase.Idle) Finish();
    }

    private bool TransitionDone(MovieDef shown) =>
        transitionFrames >= shown.Transition.Cover + shown.Transition.Hold + shown.Transition.Clear;

    private void SetUp(MovieDef shown)
    {
        sceneUp = true;
        foreach (var child in context.Root.GetChildren())
            if (child is CanvasLayer layer && layer != overlay && layer.Visible && layer.Layer is >= UiLayers.BlindOverlay and <= UiLayers.ExitMenu)
            {
                layer.Visible = false;
                hiddenLayers.Add(layer);
            }
        context.Entities.Layer.Visible = false;
        if (GodotObject.IsInstanceValid(context.Self.Body)) context.Self.Body.Visible = false;
        savedBgm = Audio.CurrentBgm;
        Audio.StopBgm();

        previousCamera = context.Root.GetViewport().GetCamera3D();
        movieCamera = new Camera3D { Far = Mathf.Max(MinFar, previousCamera?.Far ?? MinFar) };
        context.Root.AddChild(movieCamera);
        movieCamera.MakeCurrent();

        actorRoot = new Node3D();
        context.Root.AddChild(actorRoot);
        foreach (var actor in shown.Actors)
        {
            if (ActorScene(actor.Model)?.Instantiate() is not Node3D node) continue;
            var rotation = new GodotQuaternion(actor.Rotation.X, actor.Rotation.Y, actor.Rotation.Z, actor.Rotation.W).Normalized();
            node.Transform = new Transform3D(new Basis(rotation).Scaled(ToGodot(actor.Scale)), ToGodot(actor.Position));
            actorRoot.AddChild(node);
            var anim = FindAnimationPlayer(node);
            string? clip = anim?.GetAnimationList() is { Length: > 0 } clips ? clips[0] : null;
            if (anim != null && clip != null)
            {
                anim.Play(clip);
                anim.Pause();
            }
            actors.Add((actor, node, anim, clip));
        }
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is AnimationPlayer player) return player;
            if (FindAnimationPlayer(child) is { } found) return found;
        }
        return null;
    }

    private PackedScene? ActorScene(string model)
    {
        if (actorScenes.TryGetValue(model, out var cached)) return cached;
        string path = $"{ActorPath}{model}.glb";
        var scene = ResourceLoader.Exists(path) ? ResourceLoader.Load<PackedScene>(path) : null;
        actorScenes[model] = scene;
        return scene;
    }

    private void PoseScene(MovieDef shown)
    {
        if (movieCamera != null && MoviePlayback.CameraAt(shown, frame) is var index and >= 0)
        {
            var camera = shown.Cameras[index];
            var eye = ToGodot(MoviePlayback.Sample(camera.Eye, frame));
            var at = ToGodot(MoviePlayback.Sample(camera.At, frame));
            var up = ToGodot(MoviePlayback.Sample(camera.Up, frame));
            if (up.LengthSquared() < Mathf.Epsilon || (at - eye).Normalized().Cross(up.Normalized()).LengthSquared() < Mathf.Epsilon) up = Vector3.Up;
            movieCamera.Fov = camera.Fov;
            movieCamera.Near = camera.Near;
            if (!eye.IsEqualApprox(at)) movieCamera.LookAtFromPosition(eye, at, up);
        }

        foreach (var (actor, node, anim, clip) in actors)
        {
            bool shows = MoviePlayback.Shows(actor, frame);
            if (node.Visible != shows) node.Visible = shows;
            if (shows && anim != null && clip != null) anim.Seek(MoviePlayback.ActorSeconds(actor, frame), true);
        }

        float alpha = 0f;
        var colour = Colors.Black;
        foreach (var fade in shown.Fades)
        {
            float a = MoviePlayback.FadeAlpha(fade, frame);
            if (a <= alpha) continue;
            alpha = a;
            colour = new Color(fade.Color);
        }
        colour.A = alpha;
        sceneFade.Color = colour;
    }

    private void TearDown()
    {
        sceneUp = false;
        foreach (var layer in hiddenLayers)
            if (GodotObject.IsInstanceValid(layer)) layer.Visible = true;
        hiddenLayers.Clear();
        if (GodotObject.IsInstanceValid(context.Entities.Layer)) context.Entities.Layer.Visible = true;
        if (GodotObject.IsInstanceValid(context.Self.Body)) context.Self.Body.Visible = true;
        if (previousCamera != null && GodotObject.IsInstanceValid(previousCamera)) previousCamera.MakeCurrent();
        if (movieCamera != null && GodotObject.IsInstanceValid(movieCamera)) movieCamera.QueueFree();
        movieCamera = null;
        if (actorRoot != null && GodotObject.IsInstanceValid(actorRoot)) actorRoot.QueueFree();
        actorRoot = null;
        actors.Clear();
        if (savedBgm.Length > 0) Audio.BgmFile(savedBgm);
        if (sceneFade != null) sceneFade.Color = Colors.Transparent;
    }

    private void Finish()
    {
        movie = null;
        if (overlay != null) overlay.Visible = false;
    }

    private static Vector3 ToGodot(MovieVector v) => new(v.X, v.Y, v.Z);
}
