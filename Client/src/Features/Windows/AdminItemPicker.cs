using Godot;

namespace LibreKO.Features.Windows;

internal sealed class AdminItemPicker
{
    private static readonly Vector2 ResultsSize = new(600, 380);

    private readonly IItemTooltips tooltips;

    public AdminItemPicker(IItemTooltips tooltips) => this.tooltips = tooltips;

    private HudWindow window = null!;
    private ItemSearchPanel? panel;
    private Action<int, int>? picked;

    internal bool Shown => AdminPanelUi.IsOpen(window);

    internal HudWindow Window => window;

    internal void Build(CanvasLayer layer)
    {
        window = new HudWindow("admin_item_picker", "Choose an item", new Vector2(520, 110), titleIcon: UiIcons.Get("system/search"))
        {
            Visible = false,
        };
        window.Closed += Close;
        layer.AddChild(window);
    }

    internal void Open(string title, string actionText, bool withCount, Action<int, int> onPick, string query = "",
        Action<IReadOnlyList<int>>? onFamily = null)
    {
        picked = onPick;
        window.Title = title;
        panel?.QueueFree();
        panel = new ItemSearchPanel(
            tradeableOnly: false,
            actionText: actionText,
            onAction: (hit, count) => Pick(hit.Id, count),
            showTooltip: itemId => tooltips.Show(-1, UiKit.TooltipItem(itemId)),
            hideTooltip: tooltips.Hide,
            resultsSize: ResultsSize,
            showQuantity: withCount,
            onFamily: onFamily == null ? null : hits =>
            {
                Close();
                onFamily(hits.Select(hit => hit.Id).ToList());
            });
        window.Body.AddChild(panel);
        UiKit.ShowOnTop(window);
        if (query.Length > 0)
        {
            panel.SetQuery(query);
            panel.Run();
        }
        panel.FocusQuery();
    }

    private void Pick(int itemId, int count)
    {
        var then = picked;
        Close();
        then?.Invoke(itemId, count);
    }

    internal void Close()
    {
        picked = null;
        tooltips.Hide();
        AdminPanelUi.Hide(window);
    }
}
