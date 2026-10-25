using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface IAdminPlayerEditor
{
    bool Handles(byte sub);
    Task HandleAsync(UserSession session, byte sub, Packet packet);
    Task<IReadOnlyList<AdminPanelPacketWriter.FindRow>> FindPlayersAsync(string query, int cap);
}

public sealed class AdminPlayerEditor(
    SessionManager sessionManager,
    ICharacterDirectory directory,
    IGameDataService gameData,
    IItemGrantService itemGrantService,
    IUserNotificationService userNotificationService,
    ILogger<AdminPlayerEditor> logger) : IAdminPlayerEditor
{
    public const byte ReqPlayerSheet = 45;
    public const byte ReqInventoryEdit = 46;
    public const byte AckPlayerSheet = 0x1F;

    public const byte EditPut = 1;
    public const byte EditDelete = 2;
    public const byte EditGive = 3;

    public const int SheetSlots = InventoryConstants.SlotMax + InventoryConstants.HaveMax;
    private const int EditBodySize = sizeof(int) + 1 + 1 + sizeof(int) + sizeof(ushort);

    public bool Handles(byte sub) => sub is ReqPlayerSheet or ReqInventoryEdit;

    public Task HandleAsync(UserSession session, byte sub, Packet packet) => sub switch
    {
        ReqPlayerSheet when packet.RemainingBytes >= sizeof(int) => SendSheetAsync(session, packet.ReadInt()),
        ReqInventoryEdit => HandleEditAsync(session, packet),
        _ => Task.CompletedTask,
    };

    public async Task<IReadOnlyList<AdminPanelPacketWriter.FindRow>> FindPlayersAsync(string query, int cap)
    {
        var byId = int.TryParse(query, out var wanted);
        var rows = new Dictionary<int, AdminPanelPacketWriter.FindRow>();
        foreach (var online in sessionManager.GetAll())
        {
            if ((byId && online.CharacterId == wanted) || online.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                rows[online.CharacterId] = new AdminPanelPacketWriter.FindRow(
                    online.CharacterId, 0, online.Name, online.Level, online.ZoneId,
                    AdminPanelPacketWriter.WireCoordinate(online.X), AdminPanelPacketWriter.WireCoordinate(online.Z),
                    false, online.IsBot, Online: true, Class: online.Class, Nation: (byte)online.Nation);
        }

        foreach (var stored in await directory.SearchAsync(query, byId ? wanted : null, cap))
            rows.TryAdd(stored.Id, new AdminPanelPacketWriter.FindRow(
                stored.Id, 0, stored.Name, stored.Level, stored.Zone,
                AdminPanelPacketWriter.WireCoordinate(stored.X), AdminPanelPacketWriter.WireCoordinate(stored.Z), false, false,
                Online: false, Class: stored.Class, Nation: (byte)ClassIdHelper.GetNation(stored.Class), LastSeen: UnixSeconds(stored.LastOnline)));

        return rows.Values
            .OrderByDescending(row => row.Online)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task SendSheetAsync(UserSession gm, int characterId)
    {
        var online = sessionManager.GetByCharacterId(characterId);
        AdminPanelPacketWriter.PlayerSheet sheet;
        if (online != null)
        {
            sheet = online.WithLock(target => new AdminPanelPacketWriter.PlayerSheet(
                target.CharacterId, target.Name, true, target.Level, target.Class, (byte)target.Nation, target.ZoneId,
                AdminPanelPacketWriter.WireCoordinate(target.X), AdminPanelPacketWriter.WireCoordinate(target.Z),
                target.Money, target.KnightCash, target.Loyalty, 0, SlotsOf(target.Inventory)));
        }
        else
        {
            var stored = await directory.SheetAsync(characterId);
            if (stored == null)
            {
                await SendResultAsync(gm, false, "That character no longer exists.");
                return;
            }

            var character = stored.Character;
            sheet = new AdminPanelPacketWriter.PlayerSheet(
                character.Id, character.Name, false, character.Level, character.Class, (byte)ClassIdHelper.GetNation(character.Class),
                character.Zone, AdminPanelPacketWriter.WireCoordinate(character.X), AdminPanelPacketWriter.WireCoordinate(character.Z),
                stored.Gold, stored.KnightCash, stored.Loyalty,
                UnixSeconds(character.LastOnline), SlotsOf(stored.Items));
        }

        await gm.Client.SendPacket(AdminPanelPacketWriter.PlayerSheetPacket(AckPlayerSheet, sheet));
    }

    private async Task HandleEditAsync(UserSession gm, Packet packet)
    {
        if (packet.RemainingBytes < EditBodySize)
            return;

        var characterId = packet.ReadInt();
        var action = packet.ReadByte();
        var position = packet.ReadByte();
        var itemId = packet.ReadInt();
        int count = packet.ReadUShort();

        var target = sessionManager.GetByCharacterId(characterId);
        if (target == null)
        {
            await SendResultAsync(gm, false, "The bag can only be changed while the player is online.");
            return;
        }

        if (target.Trade.IsTrading || target.Trade.IsMerchanting || target.IsGathering)
        {
            await SendResultAsync(gm, false, $"{target.Name} is trading, merchanting or gathering; try again when they stop.");
            return;
        }

        var problem = action switch
        {
            EditPut => Put(target, position, itemId, count),
            EditDelete => Delete(target, position),
            EditGive => await GiveAsync(target, itemId, count),
            _ => "Unknown change.",
        };
        if (problem != null)
        {
            await SendResultAsync(gm, false, problem);
            return;
        }

        logger.LogInformation("GM {Gm} changed the bag of {Target}: action {Action} slot {Slot} item {ItemId} x{Count}",
            gm.Name, target.Name, action, position, itemId, count);
        await target.Client.SendPacket(ItemMovePacketMapper.BuildArrangedResponse(target));
        await userNotificationService.SendWeightChangeAsync(target);
        await SendSheetAsync(gm, characterId);
    }

    private string? Put(UserSession target, byte position, int itemId, int count)
    {
        if (!IsBagPosition(position))
            return "Only bag slots can be changed here.";
        if (gameData.GetItem(itemId) is not { } item)
            return $"Item {itemId} does not exist.";

        return target.WithLock(session =>
        {
            var slot = session.Inventory[position];
            if (slot.IsLinked)
                return "That item is linked to a familiar or a sealed character; it cannot be replaced here.";
            slot.Clear();
            slot.ItemId = item.Num;
            slot.Durability = item.Duration;
            slot.Count = (ushort)(item.Countable == 0 ? 1 : Math.Clamp(count, 1, InventoryConstants.MaxStackCount));
            session.RecalculateStatsWithBuffs(gameData);
            return null;
        });
    }

    private string? Delete(UserSession target, byte position)
    {
        if (!IsBagPosition(position))
            return "Only bag slots can be changed here.";

        return target.WithLock(session =>
        {
            var slot = session.Inventory[position];
            if (slot.IsEmpty)
                return "That slot is already empty.";
            if (slot.IsLinked)
                return "That item is linked to a familiar or a sealed character; it cannot be deleted here.";
            slot.Clear();
            session.RecalculateStatsWithBuffs(gameData);
            return null;
        });
    }

    private async Task<string?> GiveAsync(UserSession target, int itemId, int count)
    {
        if (gameData.GetItem(itemId) is not { } item)
            return $"Item {itemId} does not exist.";
        var granted = await itemGrantService.GrantAsync(target, item, Math.Clamp(count, 1, InventoryConstants.MaxStackCount));
        return granted > 0 ? null : $"{target.Name}'s bag has no room for it.";
    }

    private static bool IsBagPosition(byte position) =>
        position >= InventoryConstants.InventoryStart && position < InventoryConstants.InventoryStart + InventoryConstants.HaveMax;

    private static List<AdminPanelPacketWriter.SheetSlot> SlotsOf(ItemSlot[] inventory)
    {
        var slots = new List<AdminPanelPacketWriter.SheetSlot>();
        for (var position = 0; position < SheetSlots && position < inventory.Length; position++)
        {
            var slot = inventory[position];
            if (slot == null || slot.IsEmpty)
                continue;
            slots.Add(new AdminPanelPacketWriter.SheetSlot((byte)position, slot.ItemId, slot.Count, slot.Durability, slot.Flag));
        }

        return slots;
    }

    private static uint UnixSeconds(DateTime? at) =>
        at is { } when ? (uint)Math.Clamp(new DateTimeOffset(DateTime.SpecifyKind(when, DateTimeKind.Utc)).ToUnixTimeSeconds(), 0, uint.MaxValue) : 0;

    private static Task SendResultAsync(UserSession session, bool ok, string message) =>
        session.Client.SendPacket(AdminPanelPacketWriter.Result(ok, message));
}
