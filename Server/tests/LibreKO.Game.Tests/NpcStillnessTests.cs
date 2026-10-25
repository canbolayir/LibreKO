using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.Tests;

public class NpcStillnessTests : GameTestBase
{
    private const byte Moradon = 21;

    private static NpcInstance Wanderer(byte searchRange, byte idleSpeed) => new()
    {
        IsMonster = true,
        ZoneId = Moradon,
        X = 60f,
        Z = 60f,
        SpawnX = 60f,
        SpawnZ = 60f,
        MoveType = NpcMoveType.Wander,
        InitMoveType = NpcMoveType.Wander,
        SearchRange = searchRange,
        Speed1 = idleSpeed,
        Hp = 100,
        MaxHp = 100,
    };

    [Fact]
    public async Task AWandererWithNoSearchRangeStaysPut()
    {
        using var provider = CreateProvider(_ => { });
        var npc = provider.GetRequiredService<SessionManager>().Regions.SpawnNpc(Wanderer(searchRange: 0, idleSpeed: 4));

        await provider.GetRequiredService<INpcAiMovementService>().StartIdleMovementAsync(npc, DateTime.UtcNow.Ticks);

        npc.State.Should().NotBe(NpcState.Moving);
    }

    [Fact]
    public async Task AWandererWithNoSpeedNeverLeavesItsSpot()
    {
        using var provider = CreateProvider(_ => { });
        var movement = provider.GetRequiredService<INpcAiMovementService>();
        var npc = provider.GetRequiredService<SessionManager>().Regions.SpawnNpc(Wanderer(searchRange: 10, idleSpeed: 0));

        await movement.StartIdleMovementAsync(npc, DateTime.UtcNow.Ticks);
        for (var tick = 0; tick < 20; tick++)
            await movement.HandleIdleMovingAsync(npc, DateTime.UtcNow.Ticks);

        npc.X.Should().Be(60f);
        npc.Z.Should().Be(60f);
    }

    [Fact]
    public void ABorderMonumentTakesTenFromABasicHitAndNothingFromSkills()
    {
        NpcHitRules.Taken(NpcData.TypeBorderMonument, 340, basicHit: true).Should().Be(NpcHitRules.BorderMonumentHit);
        NpcHitRules.Taken(NpcData.TypeBorderMonument, 0, basicHit: true).Should().Be(0);
        NpcHitRules.Taken(NpcData.TypeBorderMonument, 900, basicHit: false).Should().Be(0);
        NpcHitRules.Taken(NpcData.TypeMonster, 340, basicHit: false).Should().Be(340);
    }

    [Fact]
    public void TheBarracksEventMonstersTakeOneFromABasicHitAndNothingFromSkills()
    {
        NpcHitRules.Taken(NpcData.TypeBarracksEvent, 340, basicHit: true).Should().Be(NpcHitRules.BarracksEventHit);
        NpcHitRules.Taken(NpcData.TypeBarracksEvent, 900, basicHit: false).Should().Be(0);
    }
}
