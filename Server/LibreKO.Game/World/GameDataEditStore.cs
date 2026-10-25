using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.World;

public interface IGameDataEditStore
{
    Task<bool> SaveMonsterAsync(NpcData proto);
    Task SaveDropRowAsync(NpcItemData row);
    Task SaveItemGroupAsync(int group, int[] items);
}

public sealed class GameDataEditStore(IServiceScopeFactory scopeFactory) : IGameDataEditStore
{
    public async Task<bool> SaveMonsterAsync(NpcData proto)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Npcs.FindAsync(proto.Id, proto.IsMonster);
        if (stored == null)
            return false;

        MonsterSheet.CopyTo(proto, stored);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task SaveDropRowAsync(NpcItemData row)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.NpcItems.FindAsync(row.Index, row.IsMonster);
        if (stored == null)
        {
            stored = new NpcItemData { Index = row.Index, IsMonster = row.IsMonster };
            db.NpcItems.Add(stored);
        }

        var drops = row.GetDrops();
        for (var slot = 0; slot < NpcItemData.DropSlots; slot++)
            stored.SetDrop(slot, drops[slot].ItemId, drops[slot].Percent);
        await db.SaveChangesAsync();
    }

    public async Task SaveItemGroupAsync(int group, int[] items)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.MakeItemGroups.FindAsync(group);
        if (stored == null)
        {
            stored = new MakeItemGroupData { GroupNum = group };
            db.MakeItemGroups.Add(stored);
        }

        stored.Items = string.Join(',', items);
        await db.SaveChangesAsync();
    }
}
