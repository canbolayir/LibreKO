using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface IAdminMonsterEditor
{
    bool Handles(byte sub);
    Task HandleAsync(UserSession session, byte sub, Packet packet);
}

public sealed class AdminMonsterEditor(
    SessionManager sessionManager,
    IGameDataService gameData,
    IGameDataEditStore store,
    TimeWeatherBroadcastService timeWeather,
    ILogger<AdminMonsterEditor> logger) : IAdminMonsterEditor
{
    public const byte ReqMonster = 21;
    public const byte ReqMonsterSet = 22;
    public const byte ReqMonsterPersist = 23;
    public const byte ReqDropsSet = 24;
    public const byte ReqDropsPersist = 25;
    public const byte ReqItemGroup = 26;
    public const byte ReqItemGroupSet = 27;
    public const byte ReqItemGroupPersist = 28;
    public const byte ReqDropSimulate = 29;
    public const byte ReqItemGroups = 47;

    public const byte AckMonster = 0x18;
    public const byte AckItemGroup = 0x19;
    public const byte AckDropSimulation = 0x1A;
    public const byte AckItemGroups = 0x20;
    public const int GroupListCap = 40;
    public const int GroupSampleItems = 6;

    public const byte ByTarget = 0;
    public const byte ByMonster = 1;
    public const byte ByNpc = 2;

    public const int MaxRolls = 100_000;
    public const int MaxGroupItems = 500;

    public bool Handles(byte sub) => sub is >= ReqMonster and <= ReqDropSimulate or ReqItemGroups;

    public Task HandleAsync(UserSession session, byte sub, Packet packet) => sub switch
    {
        ReqMonster => HandleSheetRequestAsync(session, packet),
        ReqMonsterSet => HandleSheetEditAsync(session, packet, persist: false),
        ReqMonsterPersist => HandleSheetEditAsync(session, packet, persist: true),
        ReqDropsSet => HandleDropsEditAsync(session, packet, persist: false),
        ReqDropsPersist => HandleDropsEditAsync(session, packet, persist: true),
        ReqItemGroup => HandleItemGroupRequestAsync(session, packet),
        ReqItemGroupSet => HandleItemGroupEditAsync(session, packet, persist: false),
        ReqItemGroupPersist => HandleItemGroupEditAsync(session, packet, persist: true),
        ReqDropSimulate => HandleSimulateAsync(session, packet),
        ReqItemGroups => HandleItemGroupListAsync(session, packet),
        _ => Task.CompletedTask,
    };

    private async Task HandleSheetRequestAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 5)
            return;

        var by = packet.ReadByte();
        var id = packet.ReadInt();
        var spawnRow = 0;
        NpcData? proto;
        if (by == ByTarget)
        {
            var npc = sessionManager.Regions.GetNpc(id);
            proto = npc == null ? null : Proto(npc.IsMonster, npc.NpcId);
            spawnRow = npc?.SpawnRow ?? 0;
        }
        else
        {
            proto = Proto(by == ByMonster, id);
        }

        if (proto == null)
        {
            await SendResultAsync(session, false, by == ByTarget ? "That target is not an NPC or a monster." : $"No {(by == ByMonster ? "monster" : "NPC")} {id}.");
            return;
        }

        await SendSheetAsync(session, proto, spawnRow);
    }

    private async Task HandleSheetEditAsync(UserSession session, Packet packet, bool persist)
    {
        if (packet.RemainingBytes < 6)
            return;

        var isMonster = packet.ReadByte() == AdminPanelPacketWriter.Granted;
        var id = packet.ReadInt();
        int count = packet.ReadByte();
        var proto = Proto(isMonster, id);
        if (proto == null)
        {
            await SendResultAsync(session, false, $"No {(isMonster ? "monster" : "NPC")} {id}.");
            return;
        }

        var edits = new List<(MonsterField Field, int Value)>(count);
        for (var i = 0; i < count && packet.RemainingBytes >= 5; i++)
        {
            var field = MonsterSheet.Find(packet.ReadByte());
            var value = packet.ReadInt();
            if (field == null || value < field.Min || value > field.Max)
            {
                await SendResultAsync(session, false, field == null ? "Unknown monster field." : $"{field.Label} must be {field.Min} to {field.Max}.");
                return;
            }

            edits.Add((field, value));
        }

        var living = Living(proto).ToList();
        foreach (var (field, value) in edits)
        {
            field.Write(proto, value);
            if (field.Live != null)
                foreach (var npc in living)
                    field.Live(npc, value);
        }

        if (persist && !await store.SaveMonsterAsync(proto))
        {
            await SendResultAsync(session, false, $"{proto.Name} set but not saved: the database has no row for {proto.Id}.");
            await SendSheetAsync(session, proto, 0);
            return;
        }

        await SendResultAsync(session, true, persist
            ? $"{proto.Name} saved to the database; {living.Count} living updated (speeds, ranges and attack delay apply on the next spawn)."
            : $"{proto.Name} set on this server; {living.Count} living updated; not saved.");
        if (persist)
            logger.LogInformation("GM {Name} saved monster sheet {Id} ({Monster})", session.Name, proto.Id, proto.Name);
        await SendSheetAsync(session, proto, 0);
    }

    private async Task HandleDropsEditAsync(UserSession session, Packet packet, bool persist)
    {
        if (packet.RemainingBytes < 7 + NpcItemData.DropSlots * 6)
            return;

        var isMonster = packet.ReadByte() == AdminPanelPacketWriter.Granted;
        var id = packet.ReadInt();
        var dropRow = packet.ReadShort();
        var row = new NpcItemData { Index = dropRow, IsMonster = isMonster };
        for (var slot = 0; slot < NpcItemData.DropSlots; slot++)
        {
            var value = packet.ReadInt();
            var percent = packet.ReadShort();
            if (DropProblem(value, percent) is { } problem)
            {
                await SendResultAsync(session, false, $"Slot {slot + 1}: {problem}");
                return;
            }

            row.SetDrop(slot, value, percent);
        }

        if (dropRow <= 0)
        {
            await SendResultAsync(session, false, "Give the monster a drop row first (Drop row in its properties).");
            return;
        }

        gameData.PutNpcItem(row);
        if (persist)
        {
            await store.SaveDropRowAsync(row);
            logger.LogInformation("GM {Name} saved drop row {Row} ({Kind})", session.Name, dropRow, isMonster ? "monster" : "npc");
        }

        await SendResultAsync(session, true, persist ? $"Drop row {dropRow} saved to the database." : $"Drop row {dropRow} set on this server; not saved.");
        if (Proto(isMonster, id) is { } proto)
            await SendSheetAsync(session, proto, 0);
    }

    private string? DropProblem(int value, short percent)
    {
        if (percent < 0 || percent > DropRoll.PercentScale)
            return $"the chance must be 0 to {DropRoll.PercentScale}.";
        if (value == 0)
            return null;
        if (value >= DropRoll.FirstItemId)
            return gameData.GetItem(value) == null ? $"no item {value}." : null;
        return DropRoll.IsGroup(value) ? null : "an item id or an item group (100 and up).";
    }

    private async Task HandleItemGroupRequestAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 4)
            return;

        await SendItemGroupAsync(session, packet.ReadInt());
    }

    private async Task HandleItemGroupEditAsync(UserSession session, Packet packet, bool persist)
    {
        if (packet.RemainingBytes < 6)
            return;

        var group = packet.ReadInt();
        int count = packet.ReadUShort();
        if (!DropRoll.IsGroup(group) || count > MaxGroupItems)
        {
            await SendResultAsync(session, false, $"An item group is 100 to {DropRoll.FirstItemId - 1} and holds at most {MaxGroupItems} items.");
            return;
        }

        var items = new int[count];
        for (var i = 0; i < count; i++)
        {
            if (packet.RemainingBytes < 4)
                return;
            items[i] = packet.ReadInt();
            if (gameData.GetItem(items[i]) == null)
            {
                await SendResultAsync(session, false, $"No item {items[i]}.");
                return;
            }
        }

        gameData.PutItemGroup(group, items);
        if (persist)
        {
            await store.SaveItemGroupAsync(group, items);
            logger.LogInformation("GM {Name} saved item group {Group} ({Count} items)", session.Name, group, items.Length);
        }

        await SendResultAsync(session, true, persist ? $"Item group {group} saved to the database." : $"Item group {group} set on this server; not saved.");
        await SendItemGroupAsync(session, group);
    }

    private async Task HandleSimulateAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 12)
            return;

        var isMonster = packet.ReadByte() == AdminPanelPacketWriter.Granted;
        var id = packet.ReadInt();
        var rolls = packet.ReadInt();
        int premiumBonus = packet.ReadByte();
        int dropEvent = packet.ReadByte();
        int coinEvent = packet.ReadByte();
        var proto = Proto(isMonster, id);
        if (proto == null || rolls < 1 || rolls > MaxRolls)
        {
            await SendResultAsync(session, false, proto == null ? $"No monster {id}." : $"Roll 1 to {MaxRolls} times.");
            return;
        }

        var row = proto.ItemGroup > 0 ? gameData.GetNpcItem(proto.ItemGroup, isMonster) : null;
        var tally = DropRoll.Tally(proto.Money, row, gameData.MakeItemGroupTable, rolls, premiumBonus, dropEvent, coinEvent,
            itemId => gameData.GetItem(itemId) != null, new Random());
        var items = tally.Items.OrderByDescending(entry => entry.Value).Select(entry => (entry.Key, entry.Value)).ToList();
        await session.Client.SendPacket(AdminMonsterPacketWriter.DropSimulation(AckDropSimulation,
            new AdminMonsterPacketWriter.Simulation(isMonster, id, rolls, tally.CoinTotal, tally.CoinMin, tally.CoinMax, tally.ItemlessKills, items)));
    }

    private NpcData? Proto(bool isMonster, int id) =>
        (isMonster ? gameData.MonsterTable : gameData.NpcTable).TryGetValue(id, out var proto) ? proto : null;

    private IEnumerable<NpcInstance> Living(NpcData proto) =>
        sessionManager.Regions.GetAllNpcs().Where(npc => npc.NpcId == proto.Id && npc.IsMonster == proto.IsMonster && !npc.IsDead);

    private async Task SendSheetAsync(UserSession session, NpcData proto, int spawnRow)
    {
        var values = MonsterSheet.Fields
            .Select(field => new AdminMonsterPacketWriter.SheetValue(field.Id, field.Label, field.Read(proto), field.Min, field.Max))
            .ToList();
        var row = proto.ItemGroup > 0 ? gameData.GetNpcItem(proto.ItemGroup, proto.IsMonster) : null;
        var drops = (row ?? new NpcItemData()).GetDrops()
            .Select(drop => new AdminMonsterPacketWriter.DropSlot(drop.ItemId, drop.Percent, DropLabel(drop.ItemId)))
            .ToList();
        var table = proto.IsMonster ? gameData.MonsterTable : gameData.NpcTable;
        var sharedBy = proto.ItemGroup > 0 ? table.Values.Count(other => other.ItemGroup == proto.ItemGroup) : 0;
        await session.Client.SendPacket(AdminMonsterPacketWriter.MonsterSheet(AckMonster, new AdminMonsterPacketWriter.Sheet(
            proto.IsMonster, proto.Id, proto.Name, proto.NpcType, spawnRow, Living(proto).Count(), values,
            proto.ItemGroup, row != null, sharedBy, drops, timeWeather.DropEventAmount, timeWeather.CoinEventAmount)));
    }

    private string DropLabel(int value)
    {
        if (value <= 0)
            return string.Empty;
        if (value >= DropRoll.FirstItemId)
            return gameData.GetItem(value)?.Name ?? $"missing item {value}";
        if (!DropRoll.IsGroup(value))
            return $"invalid {value}";
        return gameData.MakeItemGroupTable.TryGetValue(value, out var pool)
            ? $"group {value}: {pool.Length} items"
            : $"group {value}: missing";
    }

    private async Task HandleItemGroupListAsync(UserSession session, Packet packet)
    {
        var query = packet.RemainingBytes >= sizeof(short) ? packet.ReadUtf8String().Trim() : "";
        var matches = gameData.MakeItemGroupTable
            .Where(entry => GroupMatches(entry.Key, entry.Value, query))
            .OrderBy(entry => entry.Key)
            .ToList();
        var rows = matches.Take(GroupListCap)
            .Select(entry => new AdminMonsterPacketWriter.GroupRow(entry.Key, entry.Value.Length, entry.Value.Take(GroupSampleItems).ToList()))
            .ToList();
        await session.Client.SendPacket(AdminMonsterPacketWriter.ItemGroups(AckItemGroups, matches.Count, rows));
    }

    private bool GroupMatches(int group, int[] items, string query)
    {
        if (query.Length == 0)
            return true;
        if (query.All(char.IsDigit))
            return group.ToString().StartsWith(query, StringComparison.Ordinal) || (int.TryParse(query, out var itemId) && items.Contains(itemId));
        return items.Any(itemId => gameData.GetItem(itemId)?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task SendItemGroupAsync(UserSession session, int group)
    {
        var exists = gameData.MakeItemGroupTable.TryGetValue(group, out var items);
        await session.Client.SendPacket(AdminMonsterPacketWriter.ItemGroup(AckItemGroup, group, exists, items ?? []));
    }

    private static Task SendResultAsync(UserSession session, bool ok, string message) =>
        session.Client.SendPacket(AdminPanelPacketWriter.Result(ok, message));
}
