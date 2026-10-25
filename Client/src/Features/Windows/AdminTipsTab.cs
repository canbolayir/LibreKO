using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminTipsTab
{
    private const float EditorTextWidth = 460;
    private const float EditorTextHeight = 110;
    private const int RowFontSize = 13;

    private static readonly (GameLanguage Language, string Code)[] Languages =
    {
        (GameLanguage.English, "EN"), (GameLanguage.Spanish, "ES"), (GameLanguage.Chinese, "ZH"),
    };

    private readonly IAdminRequests requests;
    private readonly Action<string, string, Action> confirm;

    public AdminTipsTab(IAdminRequests requests, Action<string, string, Action> confirm)
    {
        this.requests = requests;
        this.confirm = confirm;
    }

    private CheckBox delivery = null!;
    private SpinBox interval = null!;
    private VBoxContainer list = null!;
    private HudWindow editor = null!;
    private OptionButton language = null!;
    private CheckBox enabled = null!;
    private TextEdit text = null!;
    private Label count = null!;
    private Button saveTip = null!;
    private int editId;

    internal bool EditorShown => editor != null && GodotObject.IsInstanceValid(editor) && editor.Visible;

    internal HudWindow Editor => editor;

    internal Control Build()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);

        var settings = new HBoxContainer();
        settings.AddThemeConstantOverride("separation", 8);
        box.AddChild(settings);
        delivery = UiTheme.FlatCheck("Post tips in chat", true, 14);
        settings.AddChild(delivery);
        settings.AddChild(FieldLabel("every", 44));
        interval = Spin(1, AdminTips.MaxIntervalMinutes, 84);
        settings.AddChild(interval);
        settings.AddChild(FieldLabel("minutes", 70));
        var saveSettings = Button("Save", 80);
        saveSettings.Pressed += () => requests.TipSettings(delivery.ButtonPressed, (int)interval.Value);
        settings.AddChild(saveSettings);
        settings.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var newTip = Button("New tip", 110);
        newTip.Pressed += () => Edit(null);
        settings.AddChild(newTip);

        list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        box.AddChild(list);
        return box;
    }

    internal void BuildEditor(CanvasLayer layer)
    {
        editor = new HudWindow("admin_tip_editor", "Tip", new Vector2(600, 200), titleIcon: UiIcons.Get("system/chat-circle")) { Visible = false };
        editor.Closed += CloseEditor;
        layer.AddChild(editor);
        var body = editor.Body;
        body.AddThemeConstantOverride("separation", 8);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        body.AddChild(head);
        head.AddChild(FieldLabel("Language", 80));
        language = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(90, ControlHeight) };
        foreach (var (lang, code) in Languages) language.AddItem(code, (int)lang);
        head.AddChild(language);
        enabled = UiTheme.FlatCheck("Shown to players", true, 14);
        head.AddChild(enabled);

        text = new TextEdit
        {
            CustomMinimumSize = new Vector2(EditorTextWidth, EditorTextHeight),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            PlaceholderText = "A short tip players read in chat",
        };
        text.TextChanged += OnTextChanged;
        body.AddChild(text);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        body.AddChild(actions);
        saveTip = Button("Save", 100);
        saveTip.Pressed += Save;
        actions.AddChild(saveTip);
        var cancel = Button("Cancel", 100);
        cancel.Pressed += CloseEditor;
        actions.AddChild(cancel);
        count = UiTheme.Text("", 12, UiTheme.TextLo);
        count.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        count.HorizontalAlignment = HorizontalAlignment.Right;
        count.VerticalAlignment = VerticalAlignment.Center;
        actions.AddChild(count);
    }

    internal void OnTips(AdminTips tips)
    {
        if (list == null || !GodotObject.IsInstanceValid(list)) return;
        delivery.ButtonPressed = tips.Enabled;
        interval.Value = tips.IntervalMinutes;
        UiKit.ClearChildren(list);
        if (tips.Tips.Count == 0) list.AddChild(UiTheme.Text("No tips yet.", 13, UiTheme.TextLo));
        foreach (var tip in tips.Tips) list.AddChild(Row(tip));
    }

    private Control Row(AdminTip tip)
    {
        var row = ListRow(out var line);
        var code = UiTheme.Pill(Code(tip.Language), UiTheme.GoldVivid);
        code.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        line.AddChild(code);
        var body = UiTheme.Text(tip.Text, RowFontSize, tip.Enabled ? UiTheme.TextHi : UiTheme.TextLo);
        body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.VerticalAlignment = VerticalAlignment.Center;
        body.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        body.ClipText = true;
        body.TooltipText = tip.Text;
        body.MouseFilter = Control.MouseFilterEnum.Pass;
        line.AddChild(body);
        var shown = UiTheme.FlatCheck("Shown", tip.Enabled, 13);
        shown.TooltipText = "Shown to players";
        shown.Toggled += on => requests.TipSave(tip with { Enabled = on });
        line.AddChild(shown);
        var edit = UiTheme.SmallButton("Edit", "Change this tip");
        edit.Pressed += () => Edit(tip);
        line.AddChild(edit);
        var delete = UiTheme.SmallButton("Delete", "Delete this tip");
        delete.AddThemeColorOverride("font_color", UiTheme.Bad);
        delete.Pressed += () => confirm("Delete this tip?", "Delete", () => requests.TipDelete(tip.Id));
        line.AddChild(delete);
        return row;
    }

    internal void OpenEditorForPreview(AdminTip tip) => Edit(tip);

    private void Edit(AdminTip? tip)
    {
        editId = tip?.Id ?? 0;
        editor.Title = tip == null ? "New tip" : "Tip";
        language.Select(language.GetItemIndex((int)(tip?.Language ?? GameLanguage.English)));
        enabled.ButtonPressed = tip?.Enabled ?? true;
        text.Text = tip?.Text ?? "";
        OnTextChanged();
        UiKit.ShowOnTop(editor);
        FocusLater(text);
    }

    private void Save()
    {
        string body = text.Text.Trim();
        if (body.Length == 0 || body.Length > AdminTips.MaxTextLength) return;
        requests.TipSave(new AdminTip(editId, (GameLanguage)language.GetSelectedId(), enabled.ButtonPressed, body));
        CloseEditor();
    }

    private void OnTextChanged()
    {
        int length = text.Text.Trim().Length;
        count.Text = $"{length} / {AdminTips.MaxTextLength}";
        count.AddThemeColorOverride("font_color", length > AdminTips.MaxTextLength ? UiTheme.Bad : UiTheme.TextLo);
        saveTip.Disabled = length == 0 || length > AdminTips.MaxTextLength;
    }

    internal void CloseEditor()
    {
        if (editor != null && GodotObject.IsInstanceValid(editor)) editor.Visible = false;
    }

    private static string Code(GameLanguage tipLanguage)
    {
        foreach (var (lang, code) in Languages)
            if (lang == tipLanguage) return code;
        return "?";
    }
}
