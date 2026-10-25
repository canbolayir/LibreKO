using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class ManesSurvivalFeature
{
    private const int RankCellWidth = 44;
    private const int NationCellWidth = 70;
    private const int KillsCellWidth = 60;
    private const int ClanCellWidth = 140;
    private const int NameCellWidth = 160;
    private const int ScoreCellWidth = 80;
    private const int RankRowHeight = 22;
    private const string Empty = "-";
    private static readonly Color OwnRow = new(1f, 0.85f, 0.4f, 0.16f);

    private sealed class RankLine
    {
        public Control Root = null!;
        public ColorRect Own = null!;
        public Label Rank = null!, Nation = null!, Kills = null!, Clan = null!, Name = null!, Score = null!;
    }

    internal HudWindow? Rank { get; private set; }
    private readonly RankLine[] rankLines = new RankLine[ManesSurvival.RankRows];
    private RankLine? myLine;
    private ManesRank? ranking;
    private bool rankShown;

    private void RankTick()
    {
        if (!InZone || !KeyBinds.Held(KeyAction.PkLadder) || context.Root.GetViewport()?.GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox)
        {
            if (rankShown) HideRank();
            return;
        }
        if (rankShown) return;
        ShowRank();
        requests.Rank();
    }

    internal void ShowRank()
    {
        EnsureRank();
        RenderRank();
        UiKit.ShowOnTop(Rank!);
        rankShown = true;
        Notify();
    }

    private void HideRank()
    {
        rankShown = false;
        Notify();
        if (Rank != null && GodotObject.IsInstanceValid(Rank)) Rank.Visible = false;
    }

    private void OnRank(ManesRank rank)
    {
        ranking = rank;
        if (rankShown) RenderRank();
        Notify();
    }

    internal void SetRanking(ManesRank rank) => ranking = rank;

    private void RenderRank()
    {
        if (Rank == null) return;
        string me = self.LastEnter.Name ?? "";
        var rows = ranking?.Rows ?? [];
        for (int i = 0; i < rankLines.Length; i++)
        {
            var line = rankLines[i];
            line.Rank.Text = (i + 1).ToString();
            if (i >= rows.Count)
            {
                Fill(line, Nations.NotSelected, Empty, Empty, Empty, Empty);
                line.Own.Visible = false;
                continue;
            }
            var row = rows[i];
            Fill(line, row.Nation, Number(row.Kills), row.Clan.Length == 0 ? Empty : row.Clan,
                row.Name.Length == 0 ? Empty : row.Name, Number(row.Score));
            line.Own.Visible = row.Name.Length > 0 && row.Name == me;
        }
        var mine = myLine!;
        mine.Rank.Text = ranking is { MyRank: > 0 } ? ranking.MyRank.ToString() : Text(ManesSurvival.DefeatBossText, "Defeat the Boss");
        Fill(mine, self.LastEnter.Nation, Number(ranking?.MyKills ?? 0), string.IsNullOrEmpty(self.LastEnter.ClanName) ? Empty : self.LastEnter.ClanName,
            me, Number(ranking?.MyScore ?? 0));
        Rank.ResetSize();
    }

    private static string Number(int value) => value < 0 ? Empty : value.ToString();

    private static void Fill(RankLine line, int nation, string kills, string clan, string name, string score)
    {
        line.Nation.Text = nation is Nations.Karus or Nations.ElMorad ? Nations.Name(nation) : "";
        line.Nation.AddThemeColorOverride("font_color", new Color(ManesSurvival.NationColour(nation)));
        line.Kills.Text = kills;
        line.Clan.Text = clan;
        line.Name.Text = name;
        line.Score.Text = score;
    }

    private void EnsureRank()
    {
        if (Rank != null && GodotObject.IsInstanceValid(Rank)) return;
        Rank = new HudWindow("manes_rank", "Ranking", closable: false) { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        Layer().AddChild(Rank);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        Rank.Body.AddChild(column);

        var header = RankRow();
        header.AddChild(Cell("Rank", RankCellWidth, UiTheme.TextLo));
        header.AddChild(Cell("Nation", NationCellWidth, UiTheme.TextLo));
        header.AddChild(Cell("Kills", KillsCellWidth, UiTheme.TextLo));
        header.AddChild(Cell("Clan", ClanCellWidth, UiTheme.TextLo));
        header.AddChild(Cell("Name", NameCellWidth, UiTheme.TextLo));
        header.AddChild(Cell("Score", ScoreCellWidth, UiTheme.TextLo));
        column.AddChild(header);
        for (int i = 0; i < rankLines.Length; i++)
            rankLines[i] = AddRankLine(column);
        column.AddChild(new HSeparator());
        myLine = AddRankLine(column);
        myLine.Own.Visible = true;
    }

    private static HBoxContainer RankRow()
    {
        var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, RankRowHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 6);
        return line;
    }

    private static Label Cell(string text, int width, Color colour)
    {
        var label = UiTheme.Text(text, FontSize, colour, HorizontalAlignment.Center);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.ClipText = true;
        return label;
    }

    private static RankLine AddRankLine(VBoxContainer column)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(0, RankRowHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        var own = new ColorRect { Color = OwnRow, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        own.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(own);
        var line = RankRow();
        line.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(line);
        var row = new RankLine
        {
            Root = holder,
            Own = own,
            Rank = Cell("", RankCellWidth, UiTheme.TextHi),
            Nation = Cell("", NationCellWidth, UiTheme.TextHi),
            Kills = Cell("", KillsCellWidth, UiTheme.TextHi),
            Clan = Cell("", ClanCellWidth, UiTheme.TextHi),
            Name = Cell("", NameCellWidth, UiTheme.TextHi),
            Score = Cell("", ScoreCellWidth, UiTheme.Gold),
        };
        line.AddChild(row.Rank);
        line.AddChild(row.Nation);
        line.AddChild(row.Kills);
        line.AddChild(row.Clan);
        line.AddChild(row.Name);
        line.AddChild(row.Score);
        column.AddChild(holder);
        return row;
    }
}
