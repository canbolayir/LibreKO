using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminMonsterPage
{
    private const int ChanceScale = 10_000;
    private const int DefaultChance = 100;
    private const int RollsMax = 100_000;
    private const int RollsDefault = 1_000;
    private const int BonusMax = 255;
    private const int FieldPairsPerRow = 3;
    private const float FieldLabelWidth = 130;
    private const float FieldSpinWidth = 110;
    private const float IconSize = 36;
    private const float SampleIconSize = 22;
    private const int SampleIcons = 10;
    private const float ChanceWidth = 74;
    private const float SlotActionsWidth = 360;
    private const int SimulatorTopItems = 24;
    private const float SimulatorNameWidth = 320;

    private readonly IAdminRequests requests;
    private readonly IItemTooltips tooltips;
    private readonly AdminItemPicker picker;
    private readonly AdminGroupPicker groupPicker;
    private readonly Action<int> editGroup;
    private readonly Action<string, string, Action> confirm;

    public AdminMonsterPage(IAdminRequests requests, IItemTooltips tooltips, AdminItemPicker picker, AdminGroupPicker groupPicker,
        Action<int> editGroup, Action<string, string, Action> confirm)
    {
        this.requests = requests;
        this.tooltips = tooltips;
        this.picker = picker;
        this.groupPicker = groupPicker;
        this.editGroup = editGroup;
        this.confirm = confirm;
    }

    private sealed class DropDraft
    {
        public int Value;
        public int Chance;
        public string ChanceText = "";
        public bool Valid = true;
        public int OriginalValue;
        public int OriginalChance;
        public bool Changed => Value != OriginalValue || (Value != 0 && Chance != OriginalChance);
    }

    private AdminMonsterSheet? sheet;
    private readonly List<DropDraft> drafts = new();
    private readonly Dictionary<int, int[]> groups = new();
    private readonly List<(int Id, int Original, SpinBox Spin, Label Label)> fields = new();

    private VBoxContainer root = null!;
    private Label empty = null!;
    private VBoxContainer content = null!;
    private Label title = null!;
    private Label idLabel = null!;
    private HBoxContainer chips = null!;
    private Button spawnRow = null!;
    private Button propertiesTab = null!;
    private Button dropsTab = null!;
    private VBoxContainer properties = null!;
    private VBoxContainer drops = null!;
    private GridContainer fieldGrid = null!;
    private Button applyFields = null!;
    private Button saveFields = null!;
    private Button discardFields = null!;
    private Label tableLabel = null!;
    private Label sharedChip = null!;
    private VBoxContainer slotList = null!;
    private Button applyDrops = null!;
    private Button saveDrops = null!;
    private Button discardDrops = null!;
    private SpinBox rolls = null!;
    private SpinBox premium = null!;
    private SpinBox dropEvent = null!;
    private SpinBox coinEvent = null!;
    private VBoxContainer simulation = null!;

    internal Control Build()
    {
        root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        empty = UiTheme.Text("No monster chosen.", 14, UiTheme.TextLo);
        root.AddChild(empty);

        content = new VBoxContainer { Visible = false };
        content.AddThemeConstantOverride("separation", 10);
        root.AddChild(content);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        content.AddChild(head);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 4);
        head.AddChild(titles);
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 8);
        titles.AddChild(nameRow);
        title = UiTheme.Text("", 17, UiTheme.GoldVivid);
        title.AddThemeFontOverride("font", UiTheme.Strong);
        nameRow.AddChild(title);
        idLabel = UiTheme.Text("", 12, UiTheme.TextDim);
        idLabel.VerticalAlignment = VerticalAlignment.Center;
        nameRow.AddChild(idLabel);
        chips = new HBoxContainer();
        chips.AddThemeConstantOverride("separation", 6);
        titles.AddChild(chips);
        spawnRow = Button("Spawn row", 120);
        spawnRow.TooltipText = "Open the spawn row this monster came from";
        spawnRow.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        spawnRow.Pressed += () => { if (sheet is { SpawnRow: > 0 } shown) requests.SpawnRowRequest(shown.SpawnRow); };
        head.AddChild(spawnRow);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 5);
        content.AddChild(tabs);
        propertiesTab = UiTheme.UnderlineTabButton("Properties");
        propertiesTab.Pressed += () => SelectTab(showDrops: false);
        tabs.AddChild(propertiesTab);
        dropsTab = UiTheme.UnderlineTabButton("Drops");
        dropsTab.Pressed += () => SelectTab(showDrops: true);
        tabs.AddChild(dropsTab);

        properties = new VBoxContainer();
        properties.AddThemeConstantOverride("separation", 10);
        content.AddChild(properties);
        fieldGrid = new GridContainer { Columns = FieldPairsPerRow * 2 };
        fieldGrid.AddThemeConstantOverride("h_separation", 12);
        fieldGrid.AddThemeConstantOverride("v_separation", 6);
        properties.AddChild(fieldGrid);
        var fieldActions = Row();
        properties.AddChild(fieldActions);
        applyFields = Button("Apply", 110);
        applyFields.TooltipText = "Change it on this server now";
        applyFields.Pressed += () => SendFields(persist: false);
        fieldActions.AddChild(applyFields);
        saveFields = Button("Apply and save", 150);
        saveFields.TooltipText = "Change it and save it in the database";
        saveFields.Pressed += () => confirm($"Save {sheet?.Name}'s properties in the database?", "Save", () => SendFields(persist: true));
        fieldActions.AddChild(saveFields);
        discardFields = Button("Discard", 100);
        discardFields.Pressed += ResetFields;
        fieldActions.AddChild(discardFields);

        drops = new VBoxContainer { Visible = false };
        drops.AddThemeConstantOverride("separation", 8);
        content.AddChild(drops);
        var tableRow = Row();
        drops.AddChild(tableRow);
        tableLabel = UiTheme.Text("", 13, UiTheme.TextHi);
        tableRow.AddChild(tableLabel);
        sharedChip = UiTheme.Text("", 12, UiTheme.Warning);
        tableRow.AddChild(sharedChip);
        slotList = new VBoxContainer();
        slotList.AddThemeConstantOverride("separation", 4);
        drops.AddChild(slotList);
        var dropActions = Row();
        drops.AddChild(dropActions);
        applyDrops = Button("Apply", 110);
        applyDrops.TooltipText = "Change the drops on this server now";
        applyDrops.Pressed += () => SendDrops(persist: false);
        dropActions.AddChild(applyDrops);
        saveDrops = Button("Apply and save", 150);
        saveDrops.TooltipText = "Change the drops and save them in the database";
        saveDrops.Pressed += AskSaveDrops;
        dropActions.AddChild(saveDrops);
        discardDrops = Button("Discard", 100);
        discardDrops.Pressed += ResetDrops;
        dropActions.AddChild(discardDrops);

        drops.AddChild(UiTheme.Rule());
        drops.AddChild(Heading("Drop simulator", "system/sparkle", 14));
        var inputs = Row();
        drops.AddChild(inputs);
        rolls = SimulatorField(inputs, "Kills", 1, RollsMax, RollsDefault);
        premium = SimulatorField(inputs, "Premium +%", 0, BonusMax, 0);
        dropEvent = SimulatorField(inputs, "Drop event +%", 0, BonusMax, 0);
        coinEvent = SimulatorField(inputs, "Gold event +%", 0, BonusMax, 0);
        var roll = Button("Roll", 90);
        roll.TooltipText = "Kill it this many times with the drops as they stand on the server";
        roll.Pressed += Simulate;
        inputs.AddChild(roll);
        simulation = new VBoxContainer();
        simulation.AddThemeConstantOverride("separation", 3);
        drops.AddChild(simulation);

        SelectTab(showDrops: false);
        return root;
    }

    internal void ShowDrops() => SelectTab(showDrops: true);

    private void SelectTab(bool showDrops)
    {
        this.drops.Visible = showDrops;
        properties.Visible = !showDrops;
        propertiesTab.ButtonPressed = !showDrops;
        dropsTab.ButtonPressed = showDrops;
    }

    private static HBoxContainer Row()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        return row;
    }

    private static SpinBox SimulatorField(HBoxContainer row, string label, int min, int max, int value)
    {
        var caption = UiTheme.Text(label, 13, UiTheme.TextHi);
        caption.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caption);
        var spin = Spin(min, max, 86);
        spin.Value = value;
        row.AddChild(spin);
        return spin;
    }

    internal void Show(AdminMonsterSheet shown)
    {
        bool sameMonster = sheet is { } previous && previous.ProtoId == shown.ProtoId && previous.IsMonster == shown.IsMonster;
        if (shown.SpawnRow == 0 && sameMonster) shown.SpawnRow = sheet!.SpawnRow;
        if (!sameMonster)
        {
            UiKit.ClearChildren(simulation);
            dropEvent.Value = shown.DropEventPercent;
            coinEvent.Value = shown.CoinEventPercent;
        }
        sheet = shown;
        empty.Visible = false;
        content.Visible = true;
        title.Text = shown.Name;
        idLabel.Text = $"#{shown.ProtoId}";
        UiKit.ClearChildren(chips);
        chips.AddChild(UiTheme.Pill(shown.IsMonster ? "Monster" : "NPC", UiTheme.TextHi));
        chips.AddChild(UiTheme.Pill($"type {shown.NpcType}", UiTheme.TextLo));
        chips.AddChild(UiTheme.Pill($"{shown.Alive} alive", UiTheme.TextLo));
        spawnRow.Visible = shown.SpawnRow > 0;

        ShowFields(shown.Fields);
        drafts.Clear();
        foreach (var drop in shown.Drops)
            drafts.Add(new DropDraft
            {
                Value = drop.Value, Chance = drop.Percent, OriginalValue = drop.Value, OriginalChance = drop.Percent,
                ChanceText = drop.Value > 0 ? GmPanelText.Percent(drop.Percent, ChanceScale) : "",
            });
        foreach (var draft in drafts)
            if (AdminDropSlot.IsGroup(draft.Value) && !groups.ContainsKey(draft.Value)) requests.ItemGroupRequest(draft.Value);
        RefreshDrops();
    }

    internal void OnGroup(AdminItemGroup group)
    {
        groups[group.Group] = group.Items;
        if (drafts.Any(draft => draft.Value == group.Group)) RefreshDrops();
    }

    private void ShowFields(List<AdminMonsterField> shownFields)
    {
        UiKit.ClearChildren(fieldGrid);
        fields.Clear();
        foreach (var field in shownFields)
        {
            var label = FieldLabel(field.Label, FieldLabelWidth);
            fieldGrid.AddChild(label);
            var spin = Spin(field.Min, field.Max, FieldSpinWidth);
            spin.SetValueNoSignal(field.Value);
            spin.TooltipText = $"{field.Min:n0} to {field.Max:n0}";
            fieldGrid.AddChild(spin);
            fields.Add((field.Id, field.Value, spin, label));
            spin.ValueChanged += _ => RefreshFieldState();
        }
        RefreshFieldState();
    }

    private void RefreshFieldState()
    {
        int changed = 0;
        foreach (var (_, original, spin, label) in fields)
        {
            bool isChanged = (int)spin.Value != original;
            if (isChanged) changed++;
            label.AddThemeColorOverride("font_color", isChanged ? UiTheme.GoldVivid : UiTheme.TextHi);
        }
        applyFields.Text = changed > 0 ? $"Apply ({changed})" : "Apply";
        applyFields.Disabled = changed == 0;
        saveFields.Disabled = changed == 0;
        discardFields.Disabled = changed == 0;
    }

    private void ResetFields()
    {
        foreach (var (_, original, spin, _) in fields) spin.SetValueNoSignal(original);
        RefreshFieldState();
    }

    private void SendFields(bool persist)
    {
        if (sheet is not { } shown) return;
        var values = fields.Select(field => (field.Id, (int)field.Spin.Value)).ToList();
        requests.MonsterEdit(persist, shown.IsMonster, shown.ProtoId, values);
    }

    private void RefreshDrops()
    {
        if (sheet is not { } shown) return;
        tableLabel.Text = shown.DropRow > 0 ? $"Drop table {shown.DropRow}" : "No drop table";
        sharedChip.Text = shown.DropRowSharedBy > 1 ? $"shared by {shown.DropRowSharedBy} {(shown.IsMonster ? "monsters" : "NPCs")}" : "";
        UiKit.ClearChildren(slotList);
        for (int slot = 0; slot < drafts.Count; slot++) slotList.AddChild(SlotRow(slot, drafts[slot]));
        RefreshDropActions();
    }

    private void RefreshDropActions()
    {
        bool changed = drafts.Any(draft => draft.Changed);
        bool valid = drafts.All(draft => draft.Valid);
        applyDrops.Disabled = !changed || !valid || sheet is not { DropRow: > 0 };
        saveDrops.Disabled = applyDrops.Disabled;
        discardDrops.Disabled = !changed;
    }

    private static StyleBoxFlat SlotStyle(DropDraft draft)
    {
        var style = UiTheme.Row(false);
        if (!draft.Valid) style.BorderColor = UiTheme.Bad;
        else if (draft.Changed) style.BorderColor = UiTheme.GoldVivid;
        style.SetBorderWidthAll(draft.Changed || !draft.Valid ? 2 : 1);
        return style;
    }

    private Control SlotRow(int slot, DropDraft draft)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", SlotStyle(draft));
        var line = Row();
        row.AddChild(line);

        var number = UiTheme.Text($"{slot + 1}", 12, UiTheme.TextDim);
        number.CustomMinimumSize = new Vector2(18, 0);
        number.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(number);

        if (draft.Value <= 0)
        {
            line.AddChild(Icon(null, IconSize));
            var none = UiTheme.Text("Empty slot", 13, UiTheme.TextLo);
            none.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            none.VerticalAlignment = VerticalAlignment.Center;
            line.AddChild(none);
            var emptyActions = SlotActions(line);
            emptyActions.AddChild(SlotButton("Add item", "Pick the item this slot drops", () => PickItem(slot, draft)));
            emptyActions.AddChild(SlotButton("Use a group", "This slot drops one item of an item group", () => PickGroup(slot, draft)));
            return row;
        }

        bool group = AdminDropSlot.IsGroup(draft.Value);
        line.AddChild(group ? Icon(UiIcons.Get("system/package"), IconSize) : ItemIcon(draft.Value, IconSize, tooltips));

        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 1);
        line.AddChild(names);
        if (group)
        {
            int[] items = groups.GetValueOrDefault(draft.Value) ?? [];
            names.AddChild(UiTheme.Text($"Item group {draft.Value}", 14, UiTheme.GoldBright));
            var samples = new HBoxContainer();
            samples.AddThemeConstantOverride("separation", 2);
            names.AddChild(samples);
            foreach (int item in items.Take(SampleIcons)) samples.AddChild(ItemIcon(item, SampleIconSize, tooltips));
            samples.AddChild(UiTheme.Text(items.Length > SampleIcons ? $"+{items.Length - SampleIcons} · {items.Length} items" : $"{items.Length} items", 11, UiTheme.TextLo));
        }
        else
        {
            names.AddChild(ItemName(draft.Value, 14));
            names.AddChild(IdTag(draft.Value));
        }

        var chance = new LineEdit { Text = draft.ChanceText, CustomMinimumSize = new Vector2(ChanceWidth, ControlHeight), Alignment = HorizontalAlignment.Right };
        chance.TooltipText = "Chance in percent, 0.01 to 100";
        chance.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        chance.TextChanged += text =>
        {
            draft.ChanceText = text;
            draft.Valid = GmPanelText.TryParsePercent(text, ChanceScale, out int parsed);
            if (draft.Valid) draft.Chance = parsed;
            RefreshDropButtons(row, draft);
        };
        line.AddChild(chance);
        var percent = UiTheme.Text("%", 13, UiTheme.TextLo);
        percent.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(percent);

        var actions = SlotActions(line);
        actions.AddChild(SlotButton("Change", "Drop another item from this slot", () => PickItem(slot, draft)));
        actions.AddChild(SlotButton("Use a group", "Drop one item of an item group from this slot", () => PickGroup(slot, draft)));
        if (group) actions.AddChild(SlotButton("Edit group", "Change the items of this group", () => editGroup(draft.Value)));
        var remove = SlotButton("Remove", "Empty this slot; Discard brings it back", () =>
        {
            draft.Value = 0;
            draft.Chance = 0;
            draft.ChanceText = "";
            draft.Valid = true;
            RefreshDrops();
        });
        remove.AddThemeColorOverride("font_color", UiTheme.Bad);
        actions.AddChild(remove);
        return row;
    }

    private static HBoxContainer SlotActions(HBoxContainer line)
    {
        var actions = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            CustomMinimumSize = new Vector2(SlotActionsWidth, 0),
        };
        actions.AddThemeConstantOverride("separation", 6);
        line.AddChild(actions);
        return actions;
    }

    private void RefreshDropButtons(PanelContainer row, DropDraft draft)
    {
        row.AddThemeStyleboxOverride("panel", SlotStyle(draft));
        RefreshDropActions();
    }

    private static Button SlotButton(string text, string tooltip, Action pressed)
    {
        var button = RowButton(text, tooltip, 0);
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.Pressed += pressed;
        return button;
    }

    private void PickItem(int slot, DropDraft draft) =>
        picker.Open($"Drop for slot {slot + 1}", "Pick", false, (itemId, _) =>
        {
            draft.Value = itemId;
            if (draft.Chance <= 0) SetChance(draft, DefaultChance);
            RefreshDrops();
        });

    private void PickGroup(int slot, DropDraft draft) =>
        groupPicker.Open($"Item group for slot {slot + 1}", group =>
        {
            draft.Value = group;
            if (draft.Chance <= 0) SetChance(draft, DefaultChance);
            if (!groups.ContainsKey(group)) requests.ItemGroupRequest(group);
            RefreshDrops();
        });

    private static void SetChance(DropDraft draft, int chance)
    {
        draft.Chance = chance;
        draft.ChanceText = GmPanelText.Percent(chance, ChanceScale);
        draft.Valid = true;
    }

    private void ResetDrops()
    {
        foreach (var draft in drafts)
        {
            draft.Value = draft.OriginalValue;
            draft.Chance = draft.OriginalChance;
            draft.ChanceText = draft.Value > 0 ? GmPanelText.Percent(draft.Chance, ChanceScale) : "";
            draft.Valid = true;
        }
        RefreshDrops();
    }

    private void AskSaveDrops()
    {
        if (sheet is not { } shown) return;
        string shared = shown.DropRowSharedBy > 1 ? $" It is shared by {shown.DropRowSharedBy} {(shown.IsMonster ? "monsters" : "NPCs")}." : "";
        confirm($"Save drop table {shown.DropRow} in the database?{shared}", "Save", () => SendDrops(persist: true));
    }

    private void SendDrops(bool persist)
    {
        if (sheet is not { } shown) return;
        var slots = drafts.Select(draft => (draft.Value, draft.Value > 0 ? draft.Chance : 0)).ToList();
        requests.DropsEdit(persist, shown.IsMonster, shown.ProtoId, shown.DropRow, slots);
    }

    private void Simulate()
    {
        if (sheet is not { } shown) return;
        requests.DropSimulate(shown.IsMonster, shown.ProtoId, (int)rolls.Value, (int)premium.Value, (int)dropEvent.Value, (int)coinEvent.Value);
    }

    internal void ShowSimulation(AdminDropSimulation result)
    {
        if (sheet is not { } shown || shown.ProtoId != result.ProtoId || shown.IsMonster != result.IsMonster) return;
        UiKit.ClearChildren(simulation);
        int kills = Math.Max(1, result.Rolls);
        simulation.AddChild(UiTheme.Text(
            $"{result.Rolls:n0} kills  ·  {result.CoinTotal / kills:n0} gold on average ({result.CoinMin:n0} to {result.CoinMax:n0})  ·  "
            + $"{Share(result.ItemlessKills, kills)} drop nothing", 13, UiTheme.TextHi));
        foreach (var (itemId, times) in result.Items.Take(SimulatorTopItems))
        {
            var line = Row();
            line.AddChild(ItemIcon(itemId, SampleIconSize + 4, tooltips));
            var name = ItemName(itemId, 13);
            name.CustomMinimumSize = new Vector2(SimulatorNameWidth, 0);
            line.AddChild(name);
            line.AddChild(UiTheme.Text($"× {times:n0}", 13, UiTheme.TextHi));
            line.AddChild(UiTheme.Text(Share(times, kills), 13, UiTheme.TextLo));
            simulation.AddChild(line);
        }
        if (result.Items.Count > SimulatorTopItems)
            simulation.AddChild(UiTheme.Text($"+{result.Items.Count - SimulatorTopItems} more items", 12, UiTheme.TextLo));
    }

    private static string Share(int part, int whole) => $"{GmPanelText.Percent(part, whole)} %";
}
