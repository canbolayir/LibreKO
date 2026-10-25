using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminInventoryPage
{
    private const float CellSize = 54;
    private const int BagColumns = 7;
    private const float DetailWidth = 320;
    private const float DetailIconSize = 56;
    private const int CountMax = 9_999;
    private const float GlyphShare = 0.5f;
    private static readonly Color GlyphTint = new(1, 1, 1, 0.16f);

    private static readonly Dictionary<int, string> GearNames = new()
    {
        [InventoryConstants.RightEar] = "Right earring", [InventoryConstants.Head] = "Helmet", [InventoryConstants.LeftEar] = "Left earring",
        [InventoryConstants.Neck] = "Necklace", [InventoryConstants.Breast] = "Armor", [InventoryConstants.Pet] = "Familiar",
        [InventoryConstants.RightHand] = "Right hand", [InventoryConstants.Waist] = "Belt", [InventoryConstants.LeftHand] = "Left hand",
        [InventoryConstants.RightRing] = "Right ring", [InventoryConstants.Leg] = "Pants", [InventoryConstants.LeftRing] = "Left ring",
        [InventoryConstants.Glove] = "Gloves", [InventoryConstants.Foot] = "Boots",
    };

    private readonly IAdminRequests requests;
    private readonly IItemTooltips tooltips;
    private readonly AdminItemPicker picker;
    private readonly Action<string, string, Action> confirm;

    public AdminInventoryPage(IAdminRequests requests, IItemTooltips tooltips, AdminItemPicker picker, Action<string, string, Action> confirm)
    {
        this.requests = requests;
        this.tooltips = tooltips;
        this.picker = picker;
        this.confirm = confirm;
    }

    private readonly Dictionary<int, ItemSlotView> cells = new();
    private readonly Dictionary<int, Control> placeholders = new();
    private AdminPlayerSheet? sheet;
    private int selected = -1;
    private VBoxContainer detail = null!;
    private Button give = null!;

    internal Control Build()
    {
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 22);

        var grids = new VBoxContainer();
        grids.AddThemeConstantOverride("separation", 10);
        root.AddChild(grids);

        var gearAndBag = new HBoxContainer();
        gearAndBag.AddThemeConstantOverride("separation", 26);
        grids.AddChild(gearAndBag);

        var gearColumn = new VBoxContainer();
        gearColumn.AddThemeConstantOverride("separation", 6);
        gearAndBag.AddChild(gearColumn);
        gearColumn.AddChild(Heading("Worn", "game/chest", 14));
        var gear = Grid(EquipViewLayout.Columns);
        gearColumn.AddChild(gear);
        for (int position = 0; position < InventoryConstants.SlotMax; position++) gear.AddChild(GearCell(position));

        var bagColumn = new VBoxContainer();
        bagColumn.AddThemeConstantOverride("separation", 6);
        gearAndBag.AddChild(bagColumn);
        var bagHead = new HBoxContainer();
        bagHead.AddThemeConstantOverride("separation", 10);
        bagColumn.AddChild(bagHead);
        var bagTitle = Heading("Bag", "system/bag", 14);
        bagTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bagHead.AddChild(bagTitle);
        give = Button("Give an item", 130);
        give.TooltipText = "Put an item in the first free bag slot";
        give.Pressed += Give;
        bagHead.AddChild(give);
        var bag = Grid(BagColumns);
        bagColumn.AddChild(bag);
        for (int i = 0; i < InventoryConstants.HaveMax; i++)
        {
            int position = InventoryConstants.InventoryStart + i;
            bag.AddChild(Cell(position, $"Bag slot {i + 1}"));
        }

        var detailPanel = new PanelContainer { CustomMinimumSize = new Vector2(DetailWidth, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        detailPanel.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        root.AddChild(detailPanel);
        detail = new VBoxContainer();
        detail.AddThemeConstantOverride("separation", 8);
        detailPanel.AddChild(detail);

        Show(null);
        return root;
    }

    private static GridContainer Grid(int columns)
    {
        var grid = new GridContainer { Columns = columns };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        return grid;
    }

    private Control GearCell(int position)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(CellSize, CellSize) };
        var cell = Cell(position, GearNames[position]);
        cell.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(cell);
        if (UiIcons.Equipment(position) is not { } texture) return holder;
        float side = CellSize * GlyphShare;
        var glyph = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(side, side),
            Position = new Vector2((CellSize - side) * 0.5f, (CellSize - side) * 0.5f),
            Modulate = GlyphTint,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        holder.AddChild(glyph);
        placeholders[position] = glyph;
        return holder;
    }

    private ItemSlotView Cell(int position, string label)
    {
        var cell = new ItemSlotView(CellSize) { Index = position, TooltipText = label };
        cell.Clicked += view => Select(view.Index);
        cell.Hovered += view => { if (!view.Item.IsEmpty) tooltips.Show(-1, view.Item); };
        cell.Unhovered += _ => tooltips.Hide();
        cells[position] = cell;
        return cell;
    }

    internal void Show(AdminPlayerSheet? shown)
    {
        if (shown?.Id != sheet?.Id) selected = -1;
        sheet = shown;
        foreach (var (position, cell) in cells)
        {
            var slot = shown?.At(position);
            if (slot is { } filled) cell.Set(new ItemSlot { ItemId = filled.ItemId, Count = (short)filled.Count, Durability = (short)filled.Durability, Flag = (byte)filled.Flag });
            else cell.Clear();
            if (placeholders.TryGetValue(position, out var glyph)) glyph.Visible = slot == null;
            cell.Look = position == selected ? SlotLook.Selected : SlotLook.Normal;
        }
        give.Disabled = shown is not { Online: true };
        RefreshDetail();
    }

    private bool Editable => sheet is { Online: true };

    internal void Select(int position)
    {
        selected = position;
        foreach (var (key, cell) in cells) cell.Look = key == position ? SlotLook.Selected : SlotLook.Normal;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        UiKit.ClearChildren(detail);
        if (sheet is not { } target || selected < 0) return;
        string where = GearNames.TryGetValue(selected, out var gearName) ? gearName : $"Bag slot {selected - InventoryConstants.InventoryStart + 1}";
        detail.AddChild(UiTheme.Text(where, 13, UiTheme.TextLo));

        int position = selected;
        if (target.At(position) is not { } slot)
        {
            if (Editable && InventoryConstants.IsGridSlot(position))
                AddAction(detail, "Put an item here", "Choose an item for this slot", () => picker.Open($"Put an item in {where.ToLowerInvariant()}", "Put here", true,
                    (itemId, count) => requests.InventoryEdit(target.Id, AdminInventoryEdit.Put, position, itemId, count)), 180);
            return;
        }

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        detail.AddChild(head);
        head.AddChild(ItemIcon(slot.ItemId, DetailIconSize, tooltips));
        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 2);
        head.AddChild(names);
        var name = ItemName(slot.ItemId, 14);
        name.ClipText = false;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        names.AddChild(name);
        names.AddChild(IdTag(slot.ItemId));
        if (slot.Count > 1) names.AddChild(UiTheme.Text($"× {slot.Count:n0}", 13, UiTheme.TextHi));

        if (!Editable || !InventoryConstants.IsGridSlot(position)) return;

        int itemId = slot.ItemId;
        if (UiKit.IsStackable(itemId))
        {
            var countRow = new HBoxContainer();
            countRow.AddThemeConstantOverride("separation", 6);
            detail.AddChild(countRow);
            var count = Spin(1, CountMax, ActionWidth);
            count.Value = Math.Max(1, slot.Count);
            countRow.AddChild(count);
            AddAction(countRow, "Set count", "Change how many are in this slot",
                () => requests.InventoryEdit(target.Id, AdminInventoryEdit.Put, position, itemId, (int)count.Value));
        }

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        detail.AddChild(actions);
        AddAction(actions, "Replace", "Put another item in this slot", () => picker.Open($"Replace {ItemData.DisplayName(itemId)}", "Put in its place", true,
            (newItem, count) => requests.InventoryEdit(target.Id, AdminInventoryEdit.Put, position, newItem, count)));
        var delete = AddAction(actions, "Delete", "Take this item out of the bag", () =>
            confirm($"Delete {ItemData.DisplayName(itemId)} from {target.Name}'s bag?", "Delete",
                () => requests.InventoryEdit(target.Id, AdminInventoryEdit.Delete, position, 0, 0)));
        delete.AddThemeColorOverride("font_color", UiTheme.Bad);
    }

    private void Give()
    {
        if (sheet is not { Online: true } target) return;
        picker.Open($"Give an item to {target.Name}", "Give", true,
            (itemId, count) => requests.InventoryEdit(target.Id, AdminInventoryEdit.Give, 0, itemId, count));
    }
}
