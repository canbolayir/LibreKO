using Godot;
using static LibreKO.Features.Windows.AdminPanelUi;

namespace LibreKO.Features.Windows;

internal sealed class AdminRowList<TRow> where TRow : class
{
    private const float RemoveWidth = 34;

    private readonly VBoxContainer rows = new();
    private readonly List<(TRow Row, HBoxContainer Line)> lines = new();
    private readonly Func<HBoxContainer, TRow> build;
    private readonly int max;
    private readonly Button add;

    public AdminRowList(Container parent, string addText, int max, Func<HBoxContainer, TRow> build)
    {
        this.build = build;
        this.max = max;
        rows.AddThemeConstantOverride("separation", 4);
        parent.AddChild(rows);
        add = Button(addText, 130);
        add.Pressed += () => Add();
        parent.AddChild(add);
    }

    internal IReadOnlyList<TRow> Rows => lines.Select(line => line.Row).ToList();

    internal TRow Add()
    {
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 6);
        rows.AddChild(line);
        var row = build(line);
        var remove = Button("✕", RemoveWidth);
        remove.TooltipText = "Remove this row";
        remove.Pressed += () => Remove(row);
        line.AddChild(remove);
        lines.Add((row, line));
        add.Disabled = lines.Count >= max;
        return row;
    }

    internal void Clear()
    {
        foreach (var (_, line) in lines) line.QueueFree();
        lines.Clear();
        add.Disabled = false;
    }

    private void Remove(TRow row)
    {
        int index = lines.FindIndex(line => line.Row == row);
        if (index < 0) return;
        lines[index].Line.QueueFree();
        lines.RemoveAt(index);
        add.Disabled = lines.Count >= max;
    }
}
