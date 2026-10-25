using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.World;

public sealed record StoredCharacter(int Id, string Name, byte Level, short Class, byte Zone, float X, float Z, DateTime? LastOnline);

public sealed record StoredCharacterSheet(StoredCharacter Character, int Gold, int Loyalty, int KnightCash, ItemSlot[] Items);

public interface ICharacterDirectory
{
    Task<IReadOnlyList<StoredCharacter>> SearchAsync(string query, int? id, int cap);
    Task<StoredCharacterSheet?> SheetAsync(int characterId);
}

public sealed class CharacterDirectory(IServiceScopeFactory scopeFactory) : ICharacterDirectory
{
    public async Task<IReadOnlyList<StoredCharacter>> SearchAsync(string query, int? id, int cap)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Characters.AsNoTracking()
            .Where(character => character.DeletionTime == null && (character.Name.Contains(query) || (id != null && character.Id == id)))
            .OrderBy(character => character.Name)
            .Take(cap)
            .Select(character => new StoredCharacter(character.Id, character.Name, character.Level, character.Class,
                character.MapId, character.X, character.Z, character.LastOnlineTime))
            .ToListAsync();
    }

    public async Task<StoredCharacterSheet?> SheetAsync(int characterId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Characters.AsNoTracking()
            .Where(character => character.Id == characterId && character.DeletionTime == null)
            .Select(character => new
            {
                character.Id, character.Name, character.Level, character.Class, character.MapId, character.X, character.Z,
                character.LastOnlineTime, character.Money, character.Loyalty, character.Items, character.AccountId,
            })
            .FirstOrDefaultAsync();
        if (row == null)
            return null;

        var knightCash = await db.Accounts.AsNoTracking()
            .Where(account => account.Id == row.AccountId)
            .Select(account => account.KnightCash)
            .FirstOrDefaultAsync();
        var items = new ItemSlot[InventoryConstants.InventoryTotal];
        for (var index = 0; index < items.Length; index++)
            items[index] = new ItemSlot();
        UserSessionBinaryState.LoadItems(items, row.Items);
        return new StoredCharacterSheet(
            new StoredCharacter(row.Id, row.Name, row.Level, row.Class, row.MapId, row.X, row.Z, row.LastOnlineTime),
            row.Money, row.Loyalty, knightCash, items);
    }
}
