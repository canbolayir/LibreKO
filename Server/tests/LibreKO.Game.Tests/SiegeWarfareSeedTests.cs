using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Common.Infrastructure.Persistence.Seed;
using LibreKO.Common.Infrastructure.Persistence.Seed.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LibreKO.Game.Tests;

public class SiegeWarfareSeedTests : GameTestBase
{
    private const short CastleIndex = 1;
    private const short CapturedByClan = 77;
    private const short RaisedTariff = 25;

    [Fact]
    public async Task ReseedingMovesOnlyTheWarTimeAndKeepsTheCastleState()
    {
        var shipped = new SiegeWarfareSeed().GetSeedData().Single();
        using var provider = CreateProvider(db => db.SiegeWarfare.Add(new SiegeWarfareData
        {
            CastleIndex = CastleIndex,
            MasterKnights = CapturedByClan,
            MoradonTariff = RaisedTariff,
            WarDay = 0,
            WarTime = 0,
            WarMinute = 0,
        }));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await new DataSeeder(db, NullLogger<DataSeeder>.Instance).SeedEntityAsync(new SiegeWarfareSeed(), force: true);

        var row = await db.SiegeWarfare.AsNoTracking().SingleAsync();
        (row.WarDay, row.WarTime, row.WarMinute).Should().Be((shipped.WarDay, shipped.WarTime, shipped.WarMinute));
        row.MasterKnights.Should().Be(CapturedByClan);
        row.MoradonTariff.Should().Be(RaisedTariff);
    }

    [Fact]
    public async Task AnEmptyTableGetsTheWholeShippedRow()
    {
        var shipped = new SiegeWarfareSeed().GetSeedData().Single();
        using var provider = CreateProvider(_ => { });
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await new DataSeeder(db, NullLogger<DataSeeder>.Instance).SeedEntityAsync(new SiegeWarfareSeed(), force: true);

        var row = await db.SiegeWarfare.AsNoTracking().SingleAsync();
        row.MasterKnights.Should().Be(shipped.MasterKnights);
        row.MoradonTariff.Should().Be(shipped.MoradonTariff);
        row.WarDay.Should().Be(shipped.WarDay);
    }

    [Fact]
    public async Task ATableThatHoldsRowsIsLeftAloneUnlessTheSeedIsForced()
    {
        using var provider = CreateProvider(db => db.SiegeWarfare.Add(new SiegeWarfareData
        {
            CastleIndex = CastleIndex,
            MasterKnights = CapturedByClan,
            WarDay = 0,
            WarTime = 0,
            WarMinute = 0,
        }));
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = await new DataSeeder(db, NullLogger<DataSeeder>.Instance).SeedEntityAsync(new SiegeWarfareSeed());

        applied.Should().BeFalse();
        var row = await db.SiegeWarfare.AsNoTracking().SingleAsync();
        (row.WarDay, row.WarTime, row.WarMinute).Should().Be(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public async Task AnEmptyTableIsFilledWithoutForcing()
    {
        using var provider = CreateProvider(_ => { });
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = await new DataSeeder(db, NullLogger<DataSeeder>.Instance).SeedEntityAsync(new SiegeWarfareSeed());

        applied.Should().BeTrue();
        (await db.SiegeWarfare.CountAsync()).Should().Be(1);
    }
}
