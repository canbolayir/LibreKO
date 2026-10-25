using LibreKO.Common.Domain.Entities.GameData;

namespace LibreKO.Game.World;

public static class NpcHitRules
{
    public const int BorderMonumentHit = 10;
    public const int BarracksEventHit = 1;

    public static int Taken(byte npcType, int damage, bool basicHit) => npcType switch
    {
        NpcData.TypeBorderMonument => basicHit && damage > 0 ? BorderMonumentHit : 0,
        NpcData.TypeBarracksEvent => basicHit && damage > 0 ? BarracksEventHit : 0,
        _ => damage,
    };
}
