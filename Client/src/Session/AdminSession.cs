namespace LibreKO.Session;

public sealed class AdminSession
{
    public event Action<List<AdminCollectionRace>>? AdminCollectionRacesEvent;

    internal void RaiseAdminCollectionRaces(List<AdminCollectionRace> list) => AdminCollectionRacesEvent?.Invoke(list);

    public event Action<int, int, List<AdminFindHit>>? AdminFindEvent;

    internal void RaiseAdminFind(int arg1, int arg2, List<AdminFindHit> list) => AdminFindEvent?.Invoke(arg1, arg2, list);

    public event Action<AdminSpawnRow>? AdminSpawnRowEvent;

    internal void RaiseAdminSpawnRow(AdminSpawnRow adminSpawnRow) => AdminSpawnRowEvent?.Invoke(adminSpawnRow);

    public event Action<AdminMonsterSheet>? AdminMonsterSheetEvent;

    internal void RaiseAdminMonsterSheet(AdminMonsterSheet sheet) => AdminMonsterSheetEvent?.Invoke(sheet);

    public event Action<AdminItemGroup>? AdminItemGroupEvent;

    internal void RaiseAdminItemGroup(AdminItemGroup group) => AdminItemGroupEvent?.Invoke(group);

    public event Action<AdminDropSimulation>? AdminDropSimulationEvent;

    internal void RaiseAdminDropSimulation(AdminDropSimulation result) => AdminDropSimulationEvent?.Invoke(result);

    public event Action<AdminEventSchedules>? AdminEventSchedulesEvent;

    internal void RaiseAdminEventSchedules(AdminEventSchedules schedules) => AdminEventSchedulesEvent?.Invoke(schedules);

    public event Action<AdminLottery>? AdminLotteryEvent;

    internal void RaiseAdminLottery(AdminLottery lottery) => AdminLotteryEvent?.Invoke(lottery);

    public event Action<AdminRace>? AdminRaceEvent;

    internal void RaiseAdminRace(AdminRace race) => AdminRaceEvent?.Invoke(race);

    public event Action<AdminItemGroups>? AdminItemGroupsEvent;

    internal void RaiseAdminItemGroups(AdminItemGroups groups) => AdminItemGroupsEvent?.Invoke(groups);

    public event Action<AdminPlayerSheet>? AdminPlayerSheetEvent;

    internal void RaiseAdminPlayerSheet(AdminPlayerSheet sheet) => AdminPlayerSheetEvent?.Invoke(sheet);

    public event Action<AdminTips>? AdminTipsEvent;

    internal void RaiseAdminTips(AdminTips tips) => AdminTipsEvent?.Invoke(tips);

    public event Action<AdminState>? AdminStateEvent;

    internal void RaiseAdminState(AdminState adminState) => AdminStateEvent?.Invoke(adminState);

    public event Action<bool, string>? AdminResultEvent;

    internal void RaiseAdminResult(bool arg1, string arg2) => AdminResultEvent?.Invoke(arg1, arg2);

    public event Action<AdminPanelGrant>? AdminGrantEvent;

    private void RaiseAdminGrant(AdminPanelGrant adminPanelGrant) => AdminGrantEvent?.Invoke(adminPanelGrant);

    public event Action<List<LibreKO.Domain.GmCommandInfo>, bool>? AdminCommandsEvent;

    internal void RaiseAdminCommands(List<LibreKO.Domain.GmCommandInfo> list, bool arg2) => AdminCommandsEvent?.Invoke(list, arg2);

    public AdminPanelGrant PanelGrant { get; private set; }

    public bool GmSpeedGranted { get; private set; }

    internal void ApplyGrant(AdminPanelGrant panel, bool gmSpeed)
    {
        PanelGrant = panel;
        GmSpeedGranted = gmSpeed;
        RaiseAdminGrant(panel);
    }

    internal void ClearGrant()
    {
        if (PanelGrant == AdminPanelGrant.None && !GmSpeedGranted) return;
        ApplyGrant(AdminPanelGrant.None, false);
    }
}
