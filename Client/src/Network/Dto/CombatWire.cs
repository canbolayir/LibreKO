namespace LibreKO.Network;

public enum TargetHpEcho : byte
{
    None = 0,
    Poll = 1,
}

public enum AttackType : byte
{
    Melee = 1,
}

public enum RegeneSubOpcode : byte
{
    Town = 1,
    ResurrectChoice = 4,
    ResurrectAccept = 5,
}

public enum RespawnType : byte
{
    Normal = 1,
    ResurrectOffer = 4,
    Refused = 10,
}

public enum RespawnRefusal : ushort
{
    Standby = 4,
    MissingItem = 10,
}

public enum SkillBarSubOpcode : byte
{
    Save = 1,
    Load = 2,
}

public enum ResurrectionChoice : byte
{
    InPlace = 1,
    Town = 2,
}

public static class CombatWire
{
    public const int SkillBarMaxSlots = HotbarLayout.Total;
}

public readonly record struct AttackReply(int AttackType, int Result, int AttackerId, int TargetId, bool Chained,
    AttackPresentation? Shown = null);

public readonly record struct AttackPresentation(int Anim, float Rate, float Phase)
{
    public const int Size = 12;

    public void Write(Packet packet)
    {
        packet.WriteInt(Anim);
        packet.WriteFloat(Rate);
        packet.WriteFloat(Phase);
    }

    public static AttackPresentation? Read(Packet packet) =>
        packet.RemainingBytes >= Size ? new AttackPresentation(packet.ReadInt(), packet.ReadFloat(), packet.ReadFloat()) : null;
}

public readonly record struct MagicReply(int Sub, int SkillId, int CasterId, int TargetId, short[] Data);
