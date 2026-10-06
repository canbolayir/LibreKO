using Godot;
using LibreKO.Plugins;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private GameNpcPortrait? _dialogPortrait;
    private Ent? _dialogPortraitActor;
    private int _dialogPortraitModel,_dialogPortraitType,_dialogPortraitNpcId;
    private int[] _dialogPortraitGear=System.Array.Empty<int>();
    private GameNpcPortrait? DialogNpcPortrait()
    {
        if(!_npcDialogShown) return null;
        // A delayed dialogue reply can outlive the transient interaction-range target.
        int target=_npcTalkId>=0?_npcTalkId:_vendorNpcId;
        if(!_ents.TryGetValue(target,out var npc) || !npc.IsNpc || npc.Dead) return null;
        if(npc==_dialogPortraitActor && _dialogPortrait!=null && _dialogPortrait.Name==npc.Name && _dialogPortrait.Level==npc.Level
            && _dialogPortraitModel==npc.ModelId && _dialogPortraitType==npc.NpcType && _dialogPortraitNpcId==npc.NpcId
            && _dialogPortraitGear.SequenceEqual(npc.Gear)) return _dialogPortrait;
        int modelId=npc.ModelId;
        int[] gear=(int[])npc.Gear.Clone();
        int npcType=npc.NpcType,npcId=npc.NpcId;
        if(npcType==NpcTypes.FixedPose || NoWeaponNpcIds.Contains(npcId)) System.Array.Clear(gear);
        string appearance=$"{modelId}:"+string.Join(',',gear);
        _dialogPortraitActor=npc;_dialogPortraitModel=modelId;_dialogPortraitType=npcType;_dialogPortraitNpcId=npcId;
        _dialogPortraitGear=(int[])npc.Gear.Clone();
        _dialogPortrait=new GameNpcPortrait(appearance,npc.Name,npc.Level,()=>
        {
            var scene=ResolveMobScene(modelId);
            Node3D? model;
            if(scene!=null)
            {
                model=scene.Instantiate<Node3D>();ForceDoubleSidedOnce(model,scene.ResourcePath);
            }
            else
            {
                var visual=npc.Body!=null && GodotObject.IsInstanceValid(npc.Body)?npc.Body.GetNodeOrNull<Node3D>(ModelNodeName):null;
                var source=visual?.GetChildren().OfType<Node3D>().FirstOrDefault();
                model=source?.Duplicate((int)Node.DuplicateFlags.UseInstantiation) as Node3D;
                if(model==null) return null;
                model.Transform=Transform3D.Identity;
            }
            if(gear.Any(id=>id>0)) AttachWeapons(model,gear,npcType,npcId);
            return model;
        });
        return _dialogPortrait;
    }
}
