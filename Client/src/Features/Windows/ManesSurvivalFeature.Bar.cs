using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class ManesSurvivalFeature
{
    private const float BarWidth = 270f;
    private const float BarTop = 96f;
    private const float HpBarHeight = 14f;
    private const double CentreHold = 4.0;
    private const double CentreFade = 0.6;
    private const float CentreTop = 140f;
    private const double CountdownHold = 0.6;
    private const double CountdownFade = 0.1;
    private const int CountdownFontSize = 64;
    private static readonly Color HpFill = new("c0302a");

    internal PanelContainer? BossBar { get; private set; }
    private Label? bossName;
    private Label? bossTime;
    private ProgressBar? bossHp;
    private Label? bossHpText;
    private Label? scoreLabel;
    private Control? battleRow;
    private Label? battleTime;

    private double eventEndsAt = double.NegativeInfinity;
    private double nonBattleEndsAt = double.NegativeInfinity;
    private double invincibleEndsAt = double.NegativeInfinity;
    private bool warReadyShown;
    private bool invincibilityLifted;
    private int countdownShown = int.MaxValue;
    private string bossTitle = "";
    private long bossHpValue;
    private long bossMaxValue;

    internal void StartBar(int seconds)
    {
        warReadyShown = false;
        invincibilityLifted = false;
        countdownShown = int.MaxValue;
        bossTitle = "";
        bossHpValue = bossMaxValue = 0;
        EnsureBar();
        bossName!.Text = "";
        bossHp!.Value = bossHp.MaxValue;
        bossHpText!.Text = "";
        battleRow!.Visible = true;
        SetTimers(seconds);
        OnScore(0);
        BossBar!.Visible = true;
        BossBar.ResetSize();
        Notify();
    }

    private void SetTimers(int seconds)
    {
        eventEndsAt = context.Now + seconds;
        nonBattleEndsAt = context.Now + ManesSurvival.NonBattleLeft(seconds);
        invincibleEndsAt = context.Now + ManesSurvival.InvincibleLeft(seconds);
    }

    private void OnBoss(ManesBoss boss)
    {
        EnsureBar();
        bossTitle = boss.Name;
        bossHpValue = boss.Hp;
        bossMaxValue = boss.MaxHp;
        bossName!.Text = boss.Name;
        double ratio = boss.MaxHp > 0 ? (double)boss.Hp / boss.MaxHp : 0;
        bossHp!.Value = ratio * bossHp.MaxValue;
        bossHpText!.Text = $"{boss.Hp:N0} / {boss.MaxHp:N0}";
        SetTimers(boss.Seconds);
        BossBar!.Visible = true;
        Notify();
    }

    private void OnScore(int score)
    {
        Notify();
        if (scoreLabel == null || !GodotObject.IsInstanceValid(scoreLabel)) return;
        scoreLabel.Text = TextTemplate.Fill(Text(ManesSurvival.ScoreText, "My Score : %d"), score);
    }

    private void HideBar()
    {
        eventEndsAt = double.NegativeInfinity;
        if (BossBar != null && GodotObject.IsInstanceValid(BossBar)) BossBar.Visible = false;
    }

    private void BarTick()
    {
        if (BossBar == null || !GodotObject.IsInstanceValid(BossBar) || !BossBar.Visible) return;
        int left = SecondsUntil(eventEndsAt);
        if (left <= 0)
        {
            HideBar();
            return;
        }
        bossTime!.Text = left <= ManesSurvival.EventSeconds ? ManesSurvival.Clock(left) : "";

        int calm = SecondsUntil(nonBattleEndsAt);
        if (calm > 0)
        {
            battleRow!.Visible = true;
            battleTime!.Text = ManesSurvival.Clock(calm);
            if (calm <= ManesSurvival.CountdownSeconds && calm < countdownShown)
            {
                countdownShown = calm;
                caption.Show(calm.ToString(), Yellow, CountdownHold, CountdownFade, CentreTop, CountdownFontSize);
            }
        }
        else if (!warReadyShown)
        {
            warReadyShown = true;
            battleRow!.Visible = false;
            BossBar.ResetSize();
            caption.Show(Text(ManesSurvival.WarReadyText, "Manes' area has been changed to a war-ready status."), Yellow, CentreHold, CentreFade, CentreTop);
        }

        if (!invincibilityLifted && SecondsUntil(invincibleEndsAt) <= 0)
        {
            invincibilityLifted = true;
            string lifted = TextTemplate.Fill(Text(ManesSurvival.InvincibleLiftedText, "%s' invincibility has been lifted."), bossTitle);
            caption.Show(lifted, Yellow, CentreHold, CentreFade, CentreTop);
            context.Log.Notice(lifted, Yellow);
        }
    }

    private int SecondsUntil(double at) => (int)Math.Ceiling(Math.Max(0, at - context.Now));

    private void EnsureBar()
    {
        if (BossBar != null && GodotObject.IsInstanceValid(BossBar)) return;
        BossBar = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(BarWidth, 0) };
        BossBar.AddThemeStyleboxOverride("panel", QuestProgressToastFeature.ToastStyle());
        BossBar.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        BossBar.GrowHorizontal = Control.GrowDirection.Both;
        BossBar.OffsetTop = BarTop;
        Layer().AddChild(BossBar);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        BossBar.AddChild(column);

        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bossName = UiTheme.Text("", FontSize, UiTheme.GoldBright);
        bossName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bossTime = UiTheme.Text("", FontSize, UiTheme.TextHi, HorizontalAlignment.Right);
        head.AddChild(bossName);
        head.AddChild(bossTime);
        column.AddChild(head);

        var hold = new Control { CustomMinimumSize = new Vector2(0, HpBarHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        bossHp = new ProgressBar { ShowPercentage = false, MaxValue = 1000, Value = 1000, MouseFilter = Control.MouseFilterEnum.Ignore };
        bossHp.AddThemeStyleboxOverride("background", UiTheme.MeterTrack());
        bossHp.AddThemeStyleboxOverride("fill", UiTheme.MeterFill(HpFill));
        bossHp.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hold.AddChild(bossHp);
        bossHpText = UiTheme.Text("", 10, Colors.White, HorizontalAlignment.Center);
        bossHpText.VerticalAlignment = VerticalAlignment.Center;
        bossHpText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bossHpText.MouseFilter = Control.MouseFilterEnum.Ignore;
        hold.AddChild(bossHpText);
        column.AddChild(hold);

        scoreLabel = UiTheme.Text("", FontSize, UiTheme.TextHi, HorizontalAlignment.Center);
        column.AddChild(scoreLabel);

        var battle = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        battle.AddThemeConstantOverride("separation", 8);
        battle.AddChild(UiTheme.Text(Text(ManesSurvival.NonBattleText, "Non-Battle Time:"), FontSize, UiTheme.TextLo));
        battleTime = UiTheme.Text("", FontSize, Yellow);
        battle.AddChild(battleTime);
        battleRow = battle;
        column.AddChild(battle);
    }
}
