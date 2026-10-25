using Godot;
using LibreKO.Features.Hud;

namespace LibreKO.Features.Windows;

internal sealed class QuickslotSavesFeature : IFeature, IClosableWindow
{
    private readonly IWorldContext context;
    private readonly HotbarFeature hotbar;
    private readonly SelfSession self;

    public QuickslotSavesFeature(IWorldContext context, HotbarFeature hotbar, SelfSession self)
    {
        this.context = context;
        this.hotbar = hotbar;
        this.self = self;
    }

    internal const string WindowId = "quickslot_saves";
    private const string Title = "Save Quickslot";
    private const string FileKind = "quickslots";
    private const string PcNote = "*Quickslot is saved on the personal PC.";
    private const string TargetLabel = "Quickslot";
    private const string NameChangeLabel = "Name Change";
    private const int RowIconSize = 28;
    private const int EditCellSize = 40;
    private const int NoneSelected = -1;
    private static readonly Color NoteColour = new(1f, 0.25f, 0.25f);

    private CanvasLayer layer = null!;
    private VBoxContainer listBlock = null!;
    private VBoxContainer editBlock = null!;
    private readonly Button[] rowButtons = new Button[QuickslotSaves.RowsPerPage];
    private readonly TextureRect[,] rowIcons = new TextureRect[QuickslotSaves.RowsPerPage, QuickslotSaves.CellsPerSet];
    private readonly QuickslotCell[] editCells = new QuickslotCell[QuickslotSaves.CellsPerSet];
    private Label pageLabel = null!;
    private Label selectLabel = null!;
    private Label targetNumber = null!;
    private Label editName = null!;
    private QuickslotSaves saves = null!;
    private QuickslotSet? editing;
    private string path = "";
    private int loadedFor = -1;
    private int page;
    private int selected = NoneSelected;
    private int targetPage = 1;

    internal HudWindow Window { get; private set; } = null!;

    public bool IsOpen => Window is { Visible: true };

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.Boards };
        context.Root.AddChild(layer);
        Window = new HudWindow(WindowId, Title, new Vector2(380, 120)) { Visible = false };
        Window.Closed += Close;
        Window.VisibilityChanged += () =>
        {
            if (Window.Visible) Shown();
        };
        layer.AddChild(Window);

        var body = Window.Body;
        body.AddThemeConstantOverride("separation", 6);
        body.AddChild(UiTheme.Text(PcNote, 11, NoteColour));
        listBlock = new VBoxContainer();
        listBlock.AddThemeConstantOverride("separation", 6);
        body.AddChild(listBlock);
        for (int row = 0; row < QuickslotSaves.RowsPerPage; row++) listBlock.AddChild(BuildRow(row));
        listBlock.AddChild(BuildPager());
        editBlock = BuildEditBlock();
        editBlock.Visible = false;
        body.AddChild(editBlock);
        selectLabel = UiTheme.Text("", 12, UiTheme.TextLo, HorizontalAlignment.Center);
        body.AddChild(selectLabel);
        body.AddChild(BuildActions());
    }

    public void Dispose()
    {
    }

    private Control BuildRow(int row)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        var button = new Button { ToggleMode = true, FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_pressed_color", UiTheme.GoldBright);
        button.AddThemeColorOverride("font_hover_pressed_color", UiTheme.GoldBright);
        button.Pressed += () => Select(row);
        rowButtons[row] = button;
        box.AddChild(button);
        var icons = new HBoxContainer();
        icons.AddThemeConstantOverride("separation", 3);
        for (int cell = 0; cell < QuickslotSaves.CellsPerSet; cell++)
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(RowIconSize, RowIconSize) };
            frame.AddThemeStyleboxOverride("panel", UiTheme.Slot());
            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            frame.AddChild(icon);
            rowIcons[row, cell] = icon;
            icons.AddChild(frame);
        }
        box.AddChild(icons);
        return box;
    }

    private Control BuildPager()
    {
        var pager = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pager.AddThemeConstantOverride("separation", 8);
        pager.AddChild(Small("◀", () => TurnPage(-1)));
        pageLabel = UiTheme.Text("", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        pageLabel.CustomMinimumSize = new Vector2(48, 0);
        pager.AddChild(pageLabel);
        pager.AddChild(Small("▶", () => TurnPage(1)));
        return pager;
    }

    private VBoxContainer BuildEditBlock()
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 6);
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 8);
        editName = UiTheme.Text("", 13, UiTheme.GoldBright);
        editName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameRow.AddChild(editName);
        nameRow.AddChild(Small(NameChangeLabel, AskName));
        block.AddChild(nameRow);
        var cells = new HBoxContainer();
        cells.AddThemeConstantOverride("separation", 3);
        for (int cell = 0; cell < QuickslotSaves.CellsPerSet; cell++)
        {
            var slot = new QuickslotCell(cell, EditCellSize) { OnDrop = PlaceInEdit, OnClear = ClearInEdit };
            editCells[cell] = slot;
            cells.AddChild(slot);
        }
        block.AddChild(cells);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 6);
        buttons.AddChild(Small("Save", SaveEdit));
        buttons.AddChild(Small("Reset", ResetEdit));
        buttons.AddChild(Small("Cancel", CancelEdit));
        block.AddChild(buttons);
        return block;
    }

    private Control BuildActions()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(UiTheme.Text(TargetLabel, 12, UiTheme.TextLo));
        row.AddChild(Small("−", () => TurnTarget(-1)));
        targetNumber = UiTheme.Text("", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        targetNumber.CustomMinimumSize = new Vector2(18, 0);
        row.AddChild(targetNumber);
        row.AddChild(Small("+", () => TurnTarget(1)));
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(spacer);
        row.AddChild(Small("Apply", Apply));
        row.AddChild(Small("Edit", Edit));
        return row;
    }

    internal void Toggle()
    {
        if (IsOpen) Close();
        else Window.Visible = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        editing = null;
        Window.Visible = false;
    }

    private void Shown()
    {
        EnsureLoaded();
        Refresh();
    }

    private void EnsureLoaded()
    {
        if (loadedFor == self.MyCharId && saves != null) return;
        loadedFor = self.MyCharId;
        path = CharacterFiles.PathFor(FileKind, self.MyCharId);
        saves = QuickslotSaves.Parse(CharacterFiles.Read(path), EmptyNameTemplate());
        page = 0;
        selected = NoneSelected;
        editing = null;
    }

    internal void LoadForPreview(QuickslotSaves preview, int selectedSet, bool edit)
    {
        saves = preview;
        loadedFor = self.MyCharId;
        selected = selectedSet;
        editing = edit ? saves[selectedSet].Copy() : null;
        Refresh();
    }

    private static string EmptyNameTemplate() => ItemData.Text(QuickslotSaves.EmptyNameText, QuickslotSaves.EmptyNameFallback);

    private static string Text(int id, string fallback) => ItemData.Text(id, fallback);

    private void Refresh()
    {
        bool isEditing = editing != null;
        listBlock.Visible = !isEditing;
        editBlock.Visible = isEditing;
        for (int row = 0; row < QuickslotSaves.RowsPerPage; row++)
        {
            int index = QuickslotSaves.Index(page, row);
            var set = saves[index];
            rowButtons[row].Text = set.Name;
            rowButtons[row].SetPressedNoSignal(index == selected);
            for (int cell = 0; cell < QuickslotSaves.CellsPerSet; cell++)
            {
                int skillId = set.Cells[cell];
                rowIcons[row, cell].Texture = skillId == 0 ? null : SkillIcons.Get(skillId);
                rowIcons[row, cell].TooltipText = skillId == 0 ? "" : SkillData.Get(skillId)?.Name ?? "";
            }
        }
        pageLabel.Text = $"{page + 1} / {QuickslotSaves.Pages}";
        targetNumber.Text = targetPage.ToString();
        selectLabel.Text = selected == NoneSelected ? Text(QuickslotSaves.SelectText, QuickslotSaves.SelectFallback) : saves[selected].Name;
        if (editing == null) return;
        editName.Text = editing.Name;
        for (int cell = 0; cell < QuickslotSaves.CellsPerSet; cell++) editCells[cell].Set(editing.Cells[cell]);
    }

    private void Select(int row)
    {
        int index = QuickslotSaves.Index(page, row);
        selected = selected == index ? NoneSelected : index;
        Refresh();
    }

    private void TurnPage(int step)
    {
        page = Math.Clamp(page + step, 0, QuickslotSaves.Pages - 1);
        selected = NoneSelected;
        Refresh();
    }

    private void TurnTarget(int step)
    {
        targetPage = Math.Clamp(targetPage + step, 1, HotbarFeature.HotPages);
        Refresh();
    }

    private void Apply()
    {
        string title = Text(QuickslotSaves.TitleText, QuickslotSaves.TitleFallback);
        if (selected == NoneSelected)
        {
            Notice.Show(context.Root, Text(QuickslotSaves.SelectText, QuickslotSaves.SelectFallback), title);
            return;
        }
        var set = saves[selected];
        if (set.IsEmpty)
        {
            Notice.Show(context.Root, Text(QuickslotSaves.EmptySetText, QuickslotSaves.EmptySetFallback), title);
            return;
        }
        int hotbarPage = targetPage;
        string question = QuickslotSaves.ApplyQuestion(
            Text(QuickslotSaves.ApplyQuestionText, QuickslotSaves.ApplyQuestionFallback), set.Name, hotbarPage);
        var cells = (int[])set.Cells.Clone();
        Notice.Confirm(context.Root, question, "Yes", "No", () =>
        {
            if (!hotbar.ApplyQuickslot(hotbarPage - 1, cells))
                Notice.Show(context.Root, Text(QuickslotSaves.UnusableText, QuickslotSaves.UnusableFallback), title);
        }, title: title);
    }

    private void Edit()
    {
        if (selected == NoneSelected) return;
        editing = saves[selected].Copy();
        Refresh();
    }

    private void AskName()
    {
        if (editing == null) return;
        Notice.AskText(context.Root, Text(QuickslotSaves.NameAskText, QuickslotSaves.NameAskFallback), name =>
        {
            if (editing == null) return;
            editing.Name = name.Length > QuickslotSaves.MaxNameLength ? name[..QuickslotSaves.MaxNameLength] : name;
            Refresh();
        }, title: Text(QuickslotSaves.TitleText, QuickslotSaves.TitleFallback), maxLength: QuickslotSaves.MaxNameLength);
    }

    private void PlaceInEdit(int cell, int skillId, int fromCell)
    {
        if (editing == null || !SkillData.IsSkill(skillId)) return;
        if (editing.Place(cell, skillId, fromCell)) Refresh();
    }

    private void ClearInEdit(int cell)
    {
        if (editing != null && editing.Clear(cell)) Refresh();
    }

    private void SaveEdit()
    {
        if (editing == null || selected == NoneSelected) return;
        saves.Store(selected, editing);
        CharacterFiles.Write(path, saves.Write());
        editing = null;
        Refresh();
    }

    private void ResetEdit()
    {
        if (editing == null) return;
        editing.Reset();
        Refresh();
    }

    private void CancelEdit()
    {
        editing = null;
        Refresh();
    }

    private static Button Small(string text, System.Action pressed)
    {
        var button = UiTheme.SmallButton(text, "");
        button.Pressed += pressed;
        return button;
    }
}
