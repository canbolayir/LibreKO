using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public interface IRankMarkService
{
    Task AwardMonthAsync(IReadOnlyList<DailyRankPlace> finalPlaces);
    Task AwardKingAsync(string name);
}

public sealed class RankMarkService(
    SessionManager sessionManager,
    IServiceScopeFactory scopeFactory,
    IOptions<GameServerSettings> settings,
    ILogger<RankMarkService> logger) : IRankMarkService
{
    private const byte MissingBoard = 3;

    private RankMarkSettings Rules => settings.Value.RankMarks;

    public static bool EarnsMarks(byte board) => board <= RankMarks.LastBoardMark && board != MissingBoard;

    public async Task AwardMonthAsync(IReadOnlyList<DailyRankPlace> finalPlaces)
    {
        if (!Rules.Enabled)
            return;

        var awards = finalPlaces
            .Where(place => EarnsMarks(place.Board))
            .Select(place => (place.SubjectId, place.Board,
                Tier: RankMarks.TierFor(place.Place, Rules.GoldPlaces, Rules.SilverPlaces, Rules.BronzePlaces)))
            .Where(award => award.Tier != null)
            .GroupBy(award => award.SubjectId)
            .ToDictionary(group => group.Key, group => group.Select(award => (award.Board, Tier: award.Tier!.Value)).ToList());
        if (awards.Count == 0)
            return;

        var offline = new List<int>();
        foreach (var (characterId, marks) in awards)
        {
            if (sessionManager.GetByCharacterId(characterId) is { } online)
            {
                foreach (var (board, tier) in marks)
                    online.RankMarks.Award(board, tier);
                await online.Client.SendPacket(RankMarkPacketWriter.Own(online.RankMarks));
            }
            else
            {
                offline.Add(characterId);
            }
        }

        if (offline.Count > 0)
        {
            using var scope = scopeFactory.CreateScope();
            var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
            foreach (var character in await characters.GetByIds(offline))
            {
                var stored = RankMarks.Load(character.RankMarks);
                foreach (var (board, tier) in awards[character.Id])
                    stored.Award(board, tier);
                character.RankMarks = stored.Serialize();
                await characters.UpdateAsync(character);
            }
        }

        logger.LogInformation("Rank marks of the month given to {Count} character(s)", awards.Count);
    }

    public async Task AwardKingAsync(string name)
    {
        if (!Rules.Enabled || string.IsNullOrWhiteSpace(name))
            return;

        if (sessionManager.GetByName(name) is { } online)
        {
            online.RankMarks.AwardRibbon(RankMarks.KingRibbon);
            await online.Client.SendPacket(RankMarkPacketWriter.Own(online.RankMarks));
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        if (await characters.GetByName(name) is not { } character)
            return;
        var stored = RankMarks.Load(character.RankMarks);
        stored.AwardRibbon(RankMarks.KingRibbon);
        character.RankMarks = stored.Serialize();
        await characters.UpdateAsync(character);
    }
}
