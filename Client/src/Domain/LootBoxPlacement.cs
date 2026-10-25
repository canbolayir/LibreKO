using System;

namespace LibreKO.Domain;

public static class LootBoxPlacement
{
    private const float MaxOffset = 6f;
    private const float HugeRadius = 10f;
    private const float HugeScale = 2f;
    private const float NormalScale = 1f;

    public static float Offset(float radius) => Math.Min(MaxOffset, Math.Min(HugeRadius, radius));

    public static float Scale(float radius) => Math.Min(HugeRadius, radius) >= HugeRadius ? HugeScale : NormalScale;

    public static bool Ready(bool ownerKnown, bool ownerDead, double now, double deathClipEnd) =>
        !ownerKnown || (ownerDead && now >= deathClipEnd);

    public const double LifeSeconds = 20.0;
    public const double OpenGraceSeconds = 10.0;

    public static double SpawnAfterOpen(double spawnTime, double now) =>
        LifeSeconds - (now - spawnTime) < OpenGraceSeconds ? now + OpenGraceSeconds - LifeSeconds : spawnTime;
}
