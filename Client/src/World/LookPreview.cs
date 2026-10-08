using Godot;

namespace LibreKO;

public partial class LookPreview : SubViewportContainer
{
    private const float CameraDistance = 4f;
    private const float CameraHeight = 1.08f;
    private const float CameraAim = 1.08f;
    private const float CameraFov = 34f;

    private readonly Node3D _pivot = new();
    private readonly Camera3D _camera;
    private Node3D? _model;
    private float _aimHeight = CameraAim, _minimumDistance = CameraDistance;
    private bool _capeFraming;

    public LookPreview(int width, int height)
    {
        Stretch = true;
        ClipContents = true;
        CustomMinimumSize = new Vector2(width, height);
        var viewport = new SubViewport
        {
            OwnWorld3D = true,
            TransparentBg = true,
            Size = new Vector2I(width, height),
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
        };
        AddChild(viewport);
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.85f, 0.83f, 0.8f),
                AmbientLightEnergy = 0.7f,
            },
        });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35f, 25f, 0f), LightEnergy = 1.1f });
        var eye = new Vector3(0f, CameraHeight, CameraDistance);
        var aim = new Vector3(0f, CameraAim, 0f);
        _camera = new Camera3D
        {
            Fov = CameraFov,
            Current = true,
            Transform = new Transform3D(Basis.LookingAt(aim - eye, Vector3.Up), eye),
        };
        viewport.AddChild(_camera);
        viewport.AddChild(_pivot);
    }

    public void Show(int race, int face, int hairStyle, Color hairColour)
        => ShowModel(race, face, hairStyle, hairColour, System.Array.Empty<int>());

    private void ShowModel(int race, int face, int hairStyle, Color hairColour, int[] gear)
    {
        Clear();
        _capeFraming = false;
        // Kurian's wider, taller body needs its own framing, not a smaller model.
        var eye = new Vector3(0, race is 6 or 14 ? 1.35f : CameraHeight, race is 6 or 14 ? 5.6f : CameraDistance);
        var aim = new Vector3(0, race is 6 or 14 ? 1.35f : CameraAim, 0);
        _camera.Transform = new Transform3D(Basis.LookingAt(aim - eye, Vector3.Up), eye);
        _aimHeight = aim.Y; _minimumDistance = eye.Z;
        _model = CharacterPreview.Build(race, face, gear, hairStyle, hairColour);
        if (_model != null)
            _pivot.AddChild(_model);
        if (IsInsideTree()) Callable.From(FitCamera).CallDeferred();
    }

    public void ShowCape(int race, int face, int hairStyle, Color hairColour, int[] gear)
    {
        ShowModel(race, face, hairStyle, hairColour, gear);
        _capeFraming = true;
        _minimumDistance = 0;
        _pivot.Rotation = new Vector3(0, Mathf.Pi, 0);
    }

    public void SetCape(int capeId, Color dye, int race)
    {
        if (_model == null) return;
        var cape = _model.GetNodeOrNull<Cape>("Cape");
        if (!Cape.IsRenderable(capeId)) { cape?.QueueFree(); return; }
        if (cape is { } existing && !existing.IsQueuedForDeletion()) existing.SetCape(capeId, dye);
        else
        {
            if (cape != null) _model.RemoveChild(cape);
            Cape.Attach(_model, capeId, dye, race, highDetail: true);
        }
    }

    public override void _Ready() => FitCamera();

    private void FitCamera()
    {
        if (_model == null || !_model.IsInsideTree()) return;
        if (_capeFraming)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            void Height(Node node)
            {
                if (node is Cape) return;
                if (node is MeshInstance3D { Mesh: not null } mesh && mesh.IsVisibleInTree())
                    foreach (var point in CharacterFraming.PosedPoints(mesh))
                    { float y = point.Y - _pivot.GlobalPosition.Y; low = Mathf.Min(low, y); high = Mathf.Max(high, y); }
                foreach (var child in node.GetChildren()) Height(child);
            }
            Height(_model);
            if (float.IsFinite(low) && float.IsFinite(high)) _aimHeight = (low + high) * .5f;
        }
        float tangent = Mathf.Tan(Mathf.DegToRad(CameraFov * .5f));
        var size = GetChild<SubViewport>(0).Size;
        float horizontal = 1 / (tangent * size.X / size.Y * (1 - 12f / size.X));
        float vertical = 1 / (tangent * (1 - 12f / size.Y));
        float distance = _minimumDistance;
        void Visit(Node node)
        {
            if (_capeFraming && node is Cape) return;
            if (node is MeshInstance3D { Mesh: not null } mesh && mesh.IsVisibleInTree())
                foreach (var point in CharacterFraming.PosedPoints(mesh))
                {
                    var local = point - _pivot.GlobalPosition;
                    float radius = new Vector2(local.X, local.Z).Length();
                    // Bound the complete orbit once; rotating must not change the zoom.
                    distance = Mathf.Max(distance, radius * Mathf.Sqrt(1 + horizontal * horizontal));
                    distance = Mathf.Max(distance, radius + (Mathf.Abs(local.Y - _aimHeight) + .03f) * vertical);
                }
            foreach (var child in node.GetChildren()) Visit(child);
        }
        Visit(_model);
        var eye = new Vector3(0, _aimHeight, distance);
        _camera.Transform = new Transform3D(Basis.LookingAt(new Vector3(0, _aimHeight, 0) - eye, Vector3.Up), eye);
    }

    public void Turn(float degrees) => _pivot.RotateY(Mathf.DegToRad(degrees));

    public void Clear()
    {
        if (_model != null)
        {
            _pivot.RemoveChild(_model);
            _model.QueueFree();
        }
        _model = null;
    }
}
