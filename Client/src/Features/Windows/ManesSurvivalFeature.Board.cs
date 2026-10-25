using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class ManesSurvivalFeature
{
    private const string EventTitle = "Manes' Survival";

    internal PanelContainer? SignUpBoard { get; private set; }
    private Control? boardBody;
    internal Label? BoardTime { get; private set; }
    private Label? boardCount;
    private Button? boardApply;
    private Button? boardCancel;

    private ManesSurvivalSession.SignUpBoardState Board => manes.Board;

    private void OnSignUp(ManesSignUp signUp)
    {
        ApplySignUp(signUp);
        Notify();
    }

    private void ApplySignUp(ManesSignUp signUp)
    {
        if (signUp.Kind == ManesSignUpKind.Open)
        {
            OpenBoard(signUp.Seconds, signUp.Participants);
            return;
        }
        int refusal = ManesSurvival.SignUpText(signUp.Result);
        if (refusal > 0)
        {
            context.Log.Notice(Text(refusal, ""), Yellow);
            SetApplied(false);
            return;
        }
        if (signUp.Result == ManesSurvivalWire.SignUpApplied)
        {
            Board.Participants = signUp.Participants;
            if (boardCount != null) boardCount.Text = signUp.Participants.ToString();
            SetApplied(true);
        }
        else if (signUp.Result == ManesSurvivalWire.SignUpCancelled) SetApplied(false);
        if (Board.Open) ShowBoard();
    }

    internal void OpenBoard(int seconds, int participants)
    {
        Board.Participants = participants;
        Board.EndsAt = context.Now + seconds;
        Board.Open = true;
        Board.Applied = false;
        Board.Collapsed = false;
        ShowBoard();
        Audio.PlayUi(Sfx.InventoryOpen);
    }

    internal void ReopenBoard()
    {
        if (Board.Open) ShowBoard();
    }

    private void ShowBoard()
    {
        EnsureBoard();
        boardCount!.Text = Board.Participants.ToString();
        SetApplied(Board.Applied);
        boardBody!.Visible = !Board.Collapsed;
        BoardTime!.Text = ManesSurvival.Clock((int)Mathf.Max(0, (float)(Board.EndsAt - context.Now)));
        SignUpBoard!.Visible = true;
        SignUpBoard.ResetSize();
    }

    private void HideBoard()
    {
        Board.Open = false;
        Notify();
        if (SignUpBoard != null && GodotObject.IsInstanceValid(SignUpBoard)) SignUpBoard.Visible = false;
    }

    private void BoardTick()
    {
        if (!Board.Open || BoardTime == null || !GodotObject.IsInstanceValid(BoardTime)) return;
        int left = (int)Mathf.Max(0, (float)(Board.EndsAt - context.Now));
        BoardTime.Text = ManesSurvival.Clock(left);
        if (left <= 0) HideBoard();
    }

    private void SetApplied(bool applied)
    {
        Board.Applied = applied;
        if (boardApply == null || boardCancel == null) return;
        boardApply.Visible = !applied;
        boardCancel.Visible = applied;
    }

    private void EnsureBoard()
    {
        if (SignUpBoard != null && GodotObject.IsInstanceValid(SignUpBoard)) return;
        var anchor = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        Layer().AddChild(anchor);
        SignUpBoard = new PanelContainer { Visible = false };
        SignUpBoard.AddThemeStyleboxOverride("panel", QuestProgressToastFeature.ToastStyle());
        anchor.AddChild(SignUpBoard);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        SignUpBoard.AddChild(column);

        var strip = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None };
        strip.Pressed += () =>
        {
            Board.Collapsed = !Board.Collapsed;
            boardBody!.Visible = !Board.Collapsed;
            SignUpBoard.ResetSize();
        };
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 24);
        head.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var title = UiTheme.Text(EventTitle, FontSize, UiTheme.GoldBright);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        BoardTime = UiTheme.Text("", FontSize, UiTheme.TextHi);
        BoardTime.MouseFilter = Control.MouseFilterEnum.Ignore;
        head.AddChild(title);
        head.AddChild(BoardTime);
        strip.CustomMinimumSize = new Vector2(280, 30);
        strip.AddChild(head);
        column.AddChild(strip);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        boardBody = body;
        var count = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        count.AddThemeConstantOverride("separation", 20);
        count.AddChild(UiTheme.Text("Participants", FontSize, UiTheme.TextLo));
        boardCount = UiTheme.Text("0", FontSize, UiTheme.Gold);
        count.AddChild(boardCount);
        body.AddChild(count);
        boardApply = GenderChangeFeature.LookActionButton(Text(ManesSurvival.ApplyText, "Apply"), () =>
        {
            SetApplied(true);
            requests.SignUp(apply: true);
        });
        boardCancel = GenderChangeFeature.LookActionButton(Text(ManesSurvival.CancelText, "Cancel"), () =>
        {
            SetApplied(false);
            requests.SignUp(apply: false);
        });
        body.AddChild(boardApply);
        body.AddChild(boardCancel);
        column.AddChild(body);
    }
}
