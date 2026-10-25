using Godot;

namespace LibreKO.Features.Windows;

internal static class RankRibbonArt
{
    private const string Folder = "res://assets/ui/daily_rank/marks/";
    private const float RibbonWidth = 45f;
    private const float RibbonHeight = 17f;
    private const int CountFont = 9;
    private const int CountOutline = 3;
    private static readonly Color CountOutlineColour = new(0, 0, 0, 0.85f);

    private static readonly Dictionary<string, Texture2D?> Textures = new();

    internal static Texture2D? Texture(string name)
    {
        if (Textures.TryGetValue(name, out var cached)) return cached;
        string path = Folder + name + ".png";
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Textures[name] = texture;
        return texture;
    }

    internal static string Tooltip(int type)
    {
        int text = UserInfoCard.MarkTooltip(type);
        return text > 0 ? ItemData.Text(text, "") : "";
    }

    internal static Control Cell(GameRankRibbon ribbon)
    {
        var cell = new Control { CustomMinimumSize = new Vector2(RibbonWidth, RibbonHeight), TooltipText = ribbon.Tooltip };
        if (ribbon.Ribbon == null) return cell;
        var image = new TextureRect
        {
            Texture = ribbon.Ribbon,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        cell.AddChild(image);
        var count = UiTheme.Text(ribbon.Count, CountFont, UiTheme.GoldBright, HorizontalAlignment.Center);
        count.VerticalAlignment = VerticalAlignment.Center;
        count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        count.MouseFilter = Control.MouseFilterEnum.Ignore;
        count.AddThemeConstantOverride("outline_size", CountOutline);
        count.AddThemeColorOverride("font_outline_color", CountOutlineColour);
        cell.AddChild(count);
        return cell;
    }

    internal static IReadOnlyList<GameRankRibbon> Summary(IReadOnlyList<RankMark> marks)
    {
        var ribbons = new List<GameRankRibbon>();
        foreach (var cell in UserInfoCard.Summary(marks))
            ribbons.Add(new GameRankRibbon(Texture(UserInfoCard.Ribbon(cell.Type, cell.Tier, cell.Count)),
                cell.Count.ToString(), Tooltip(cell.Type)));
        return ribbons;
    }
}
