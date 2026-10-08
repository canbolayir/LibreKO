using Godot;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private FamiliarPortraitView _petPortrait = null!;
    private void RefreshPetPortrait()
    {
        if (_petPortrait == null) return;
        var actor = MyPetEntity();
        if (actor == null) { _petPortrait.SetAppearance(null); return; }
        int modelId = actor.ModelId;
        string appearance = "familiar:" + modelId;
        if (_petPortrait.Appearance?.AppearanceKey == appearance) return;
        _petPortrait.SetAppearance(new GameNpcPortrait(appearance, actor.Name, actor.Level, () =>
        {
            var scene = ResolveMobScene(modelId);
            if (scene != null)
            {
                var model = scene.Instantiate<Node3D>(); ForceDoubleSidedOnce(model, scene.ResourcePath); return model;
            }
            var visual = actor.Body != null && GodotObject.IsInstanceValid(actor.Body)
                ? actor.Body.GetNodeOrNull<Node3D>(ModelNodeName) : null;
            var source = visual?.GetChildren().OfType<Node3D>().FirstOrDefault();
            var copy = source?.Duplicate((int)Node.DuplicateFlags.UseInstantiation) as Node3D;
            if (copy != null) copy.Transform = Transform3D.Identity;
            return copy;
        }));
    }
}
