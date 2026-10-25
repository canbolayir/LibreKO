using Godot;

namespace LibreKO.Features.Windows;

internal sealed partial class PieceChangeFeature
{
    private const double BulkSpinSeconds = 0.35;
    private const int SummaryIconSize = 24;
    private const int RoundsBoxWidth = 90;
    private const string NoPiecesLeft = "No pieces left.";

    private SpinBox rounds = null!;
    private Label roundsOf = null!;
    private VBoxContainer summary = null!;
    private readonly Dictionary<int, int> bulkRewards = new();
    private bool bulk;
    private bool bulkStopAsked;
    private int bulkPiece;
    private int roundsLeft;
    private int roundsDone;
    private double bulkSpinLeft;

    private Control BuildRounds()
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(UiTheme.Text("Rounds", 13, UiTheme.TextLo));
        rounds = new SpinBox
        {
            MinValue = 1,
            MaxValue = 1,
            Value = 1,
            Rounded = true,
            CustomMinimumSize = new Vector2(RoundsBoxWidth, 0),
            TooltipText = "More than one round exchanges the pieces one after another",
        };
        row.AddChild(rounds);
        roundsOf = UiTheme.Text("", 13, UiTheme.TextLo);
        row.AddChild(roundsOf);
        return row;
    }

    private Control BuildSummary()
    {
        summary = new VBoxContainer { Visible = false };
        summary.AddThemeConstantOverride("separation", 2);
        return summary;
    }

    private int PiecesInBag(int pieceId)
    {
        if (pieceId == 0) return 0;
        int pieces = 0;
        for (int abs = Inventory.GridStart; abs < Inventory.GridStart + Inventory.GridCount && abs < self.Inventory.Length; abs++)
            if (self.Inventory[abs].ItemId == pieceId) pieces += Math.Max(1, (int)self.Inventory[abs].Count);
        return pieces;
    }

    private void RefreshRounds()
    {
        if (rounds == null) return;
        int pieces = PiecesInBag(bulk ? bulkPiece : itemId);
        rounds.MaxValue = Math.Max(1, pieces);
        if (rounds.Value > rounds.MaxValue) rounds.Value = rounds.MaxValue;
        roundsOf.Text = pieces > 0 ? $"of {pieces}" : "";
        rounds.Editable = !bulk && !busy && !spinning && pieces > 1;
    }

    private bool WantsBulk() => rounds != null && rounds.Value > 1;

    private void BeginBulk()
    {
        bulk = true;
        bulkStopAsked = false;
        bulkPiece = itemId;
        roundsLeft = (int)rounds.Value;
        roundsDone = 0;
        bulkRewards.Clear();
        summary.Visible = false;
        NextRound();
    }

    private bool StageBulkPiece()
    {
        int at = SocketSlot();
        int abs = at >= 0 && at < self.Inventory.Length && self.Inventory[at].ItemId == bulkPiece ? at : upgrade.FindBackpackSlot(bulkPiece);
        if (abs < 0) return false;
        var item = self.Inventory[abs];
        itemId = bulkPiece;
        position = abs - Inventory.GridStart;
        rewards = ItemData.PieceRewards(bulkPiece);
        socket.Set(item.ItemId, item.Count, item.Durability);
        return true;
    }

    private void NextRound()
    {
        if (!bulk) return;
        if (roundsLeft <= 0 || bulkStopAsked)
        {
            FinishBulk(null);
            return;
        }
        if (!StageBulkPiece())
        {
            FinishBulk(NoPiecesLeft);
            return;
        }
        if (BlockReason() is { } blocked)
        {
            FinishBulk(blocked);
            return;
        }
        spinning = true;
        spinClock = 0;
        bulkSpinLeft = BulkSpinSeconds;
        SetMessage($"Round {roundsDone + 1} of {roundsDone + roundsLeft}", false);
        RefreshRounds();
        RefreshActions();
    }

    private void BulkTick(double delta)
    {
        if (!bulk || !spinning || busy) return;
        bulkSpinLeft -= delta;
        if (bulkSpinLeft > 0) return;
        if (!StageBulkPiece())
        {
            FinishBulk(NoPiecesLeft);
            return;
        }
        StopSpin();
    }

    private void StopPressed()
    {
        if (!bulk)
        {
            StopSpin();
            return;
        }
        bulkStopAsked = true;
        RefreshActions();
    }

    private void BulkResult(PieceExchangeResult result)
    {
        busy = false;
        spinning = false;
        if (result.ResultCode != BifrostResult.Succeeded || result.RewardItemId == 0)
        {
            FinishBulk(result.ResultCode == BifrostResult.NoPiece
                ? NoPiecesLeft
                : ItemData.Text(TextFailed, "The divination has failed."));
            return;
        }
        foreach (var reel in resultSockets) reel.Clear();
        resultSockets[MiddleSocket].Set(result.RewardItemId, 1, 0);
        subMessage.Text = ItemData.DisplayName(result.RewardItemId);
        context.Log.Notice($"Chaotic Generator: {ItemData.DisplayName(result.RewardItemId)}");
        bulkRewards[result.RewardItemId] = bulkRewards.GetValueOrDefault(result.RewardItemId) + 1;
        roundsDone++;
        roundsLeft--;
        NextRound();
    }

    private void FinishBulk(string? reason)
    {
        bulk = false;
        spinning = false;
        busy = false;
        FillSummary(PiecesInBag(bulkPiece));
        if (StageBulkPiece()) RefreshBackpack();
        else
        {
            itemId = 0;
            position = -1;
            rewards = System.Array.Empty<int>();
            socket.Clear();
        }
        SetMessage(reason ?? $"{roundsDone} exchanges done.", reason != null);
        rounds.Value = 1;
        RefreshRounds();
        RefreshActions();
        RefreshBackpack();
    }

    private void FillSummary(int piecesLeft)
    {
        foreach (var child in summary.GetChildren()) child.QueueFree();
        summary.AddChild(UiTheme.Text($"Exchanged {roundsDone} times", 13, UiTheme.GoldBright));
        foreach (var (rewardId, count) in bulkRewards.OrderByDescending(r => r.Value).ThenBy(r => r.Key))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new TextureRect
            {
                Texture = ItemIcons.Get(rewardId),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(SummaryIconSize, SummaryIconSize),
            });
            var name = UiTheme.Text($"{ItemData.DisplayName(rewardId)} ×{count}", 12, UiTheme.TextHi);
            name.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(name);
            summary.AddChild(row);
        }
        summary.AddChild(UiTheme.Text($"Pieces left: {piecesLeft}", 12, UiTheme.TextLo));
        summary.Visible = true;
    }

    internal HudWindow Window => panel;

    internal void ShowBulkSummaryForPreview(int pieceId, IReadOnlyDictionary<int, int> rewardCounts, int piecesLeft)
    {
        bulkPiece = pieceId;
        bulkRewards.Clear();
        roundsDone = 0;
        foreach (var (rewardId, count) in rewardCounts)
        {
            bulkRewards[rewardId] = count;
            roundsDone += count;
        }
        FillSummary(piecesLeft);
        SetMessage($"{roundsDone} exchanges done.", false);
    }
}
