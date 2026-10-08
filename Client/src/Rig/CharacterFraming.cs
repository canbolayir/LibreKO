using System.Collections.Generic;
using Godot;

namespace LibreKO;

public static class CharacterFraming
{
    // Use the displayed skin rather than oversized bind-pose boxes.
    public static IEnumerable<Vector3> PosedPoints(MeshInstance3D mesh)
    {
        var skin = mesh.Skin;
        var skeleton = mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton);
        if (skin == null || skeleton == null)
        {
            for (int i = 0; i < 8; i++) yield return mesh.GlobalTransform * mesh.GetAabb().GetEndpoint(i);
            yield break;
        }
        var transforms = new Transform3D[skin.GetBindCount()];
        for (int bind = 0; bind < transforms.Length; bind++)
        {
            var name = skin.GetBindName(bind);
            int bone = name.IsEmpty ? skin.GetBindBone(bind) : skeleton.FindBone(name);
            transforms[bind] = bone >= 0 ? skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone) * skin.GetBindPose(bind) : mesh.GlobalTransform;
        }
        for (int surface = 0; surface < mesh.Mesh!.GetSurfaceCount(); surface++)
        {
            using var arrays = mesh.Mesh.SurfaceGetArrays(surface);
            var positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            int influences = positions.Length > 0 ? bones.Length / positions.Length : 0;
            for (int i = 0; i < positions.Length; i++)
            {
                var point = Vector3.Zero; float total = 0;
                for (int j = 0; j < influences; j++)
                {
                    int at = i * influences + j, bind = bones[at]; float weight = weights[at];
                    if (weight <= 0 || bind < 0 || bind >= transforms.Length) continue;
                    point += (transforms[bind] * positions[i]) * weight; total += weight;
                }
                yield return total > 0 ? point : mesh.GlobalTransform * positions[i];
            }
        }
    }
}
