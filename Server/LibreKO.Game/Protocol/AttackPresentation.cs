using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol;

public readonly record struct AttackPresentation(int Anim, float Rate, float Phase)
{
    public const int Size = 12;
    private const int MaxAnim = byte.MaxValue;
    private const float MinRate = 0.01f;
    private const float MaxRate = 10f;
    private const float MaxPhase = 5f;

    public bool IsValid =>
        Anim is >= 0 and <= MaxAnim
        && float.IsFinite(Rate) && Rate >= MinRate && Rate < MaxRate
        && float.IsFinite(Phase) && Phase >= 0f && Phase <= MaxPhase;

    public static AttackPresentation? Read(Packet packet)
    {
        if (packet.RemainingBytes < Size)
            return null;
        var shown = new AttackPresentation(packet.ReadInt(), packet.ReadFloat(), packet.ReadFloat());
        return shown.IsValid ? shown : null;
    }

    public void Write(Packet packet)
    {
        packet.WriteInt(Anim);
        packet.WriteFloat(Rate);
        packet.WriteFloat(Phase);
    }
}
