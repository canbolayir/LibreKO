using Godot;

namespace LibreKO.Features.Hud;

internal sealed class TargetGmFeature : IFeature
{
    private const float ButtonSize = 26f;

    private readonly IWorldContext context;
    private readonly AdminSession admin;
    private readonly IAdminRequests requests;
    private HBoxContainer row = null!;
    private int targetId = -1;

    public TargetGmFeature(IWorldContext context, AdminSession admin, IAdminRequests requests)
    {
        this.context = context;
        this.admin = admin;
        this.requests = requests;
    }

    public void Build() { }

    public void Dispose() { }

    internal void BuildRow(VBoxContainer box)
    {
        row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, Visible = false };
        row.AddThemeConstantOverride("separation", 4);
        box.AddChild(row);
        row.AddChild(GmButton("system/sliders", "GM: properties, drops and the drop simulator",
            () => requests.MonsterSheet(AdminMonsterBy.Target, targetId)));
        row.AddChild(GmButton("system/pin", "GM: spawn row",
            () => requests.TargetSpawnRow(targetId)));
    }

    private static Button GmButton(string icon, string tooltip, Action pressed)
    {
        var button = UiTheme.IconButton(UiIcons.Get(icon), tooltip);
        button.CustomMinimumSize = new Vector2(ButtonSize, ButtonSize);
        button.Pressed += pressed;
        return button;
    }

    internal void Sync(int id)
    {
        bool shown = id >= 0 && GameMaster && context.Entities.Views.TryGetValue(id, out var target) && target.IsNpc;
        targetId = shown ? id : -1;
        if (row.Visible != shown) row.Visible = shown;
    }

    private bool GameMaster => context.Self.IsGm || admin.PanelGrant == AdminPanelGrant.GameMaster;
}
