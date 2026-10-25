using Godot;

namespace LibreKO.Features.Windows;

internal sealed class TownRecallFeature : IFeature
{
    private readonly IWorldContext context;
    private readonly SelfSession self;
    private readonly IPlayerRequests playerRequests;
    private readonly SiegeTowerSystem siegeTower;
    private readonly IBifrostRequests bifrostRequests;

    public TownRecallFeature(IWorldContext context, SelfSession self, IPlayerRequests playerRequests, SiegeTowerSystem siegeTower, IBifrostRequests bifrostRequests)
    {
        this.context = context;
        this.self = self;
        this.playerRequests = playerRequests;
        this.siegeTower = siegeTower;
        this.bifrostRequests = bifrostRequests;
    }

    public void Dispose()
    {
    }

    private const int TextLowHp = 1903;

    private CanvasLayer layer = null!;
    private ConfirmationDialog dialog = null!;

    public void Build()
    {
        layer = new CanvasLayer { Layer = UiLayers.Prompts };
        context.Root.AddChild(layer);

        dialog = new ConfirmationDialog { Title = "Town Recall" };
        dialog.DialogText = "Recall to town?\n(You must be above 50% HP.)";
        dialog.GetOkButton().Text = "Recall";
        dialog.GetCancelButton().Text = "Cancel";
        dialog.Confirmed += Press;
        layer.AddChild(dialog);
    }

    public void TryOpen()
    {
        if (!Ready() || AskedDrakiTown()) return;
        dialog.PopupCentered();
    }

    internal void Press()
    {
        if (!Ready() || AskedDrakiTown()) return;
        Confirm();
    }

    private bool AskedDrakiTown()
    {
        if (context.Zone != DrakiTower.Zone) return false;
        Notice.Confirm(context.Root, ItemData.Text(DrakiTower.TownQuestionText, DrakiTower.TownQuestionFallback),
            "OK", "Cancel", bifrostRequests.DrakiTown);
        return true;
    }

    private bool Ready()
    {
        if (!context.Ready || context.Self.Dead || siegeTower.RefusedTownOnTower()) return false;
        if (!self.Vitals.BelowHalfHp) return true;
        context.Log.Notice(ItemData.Text(TextLowHp,
            "You cannot teleport back to town when you have half the HP or less"));
        return false;
    }

    private void Confirm()
    {
        playerRequests.GoTown();
        context.Log.Notice("Recalling to town…");
        Audio.Play(Sfx.WarpZone, context.Self.Body.GlobalPosition);
    }
}
