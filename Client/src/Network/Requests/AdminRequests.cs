namespace LibreKO.Network.Requests;

public interface IAdminRequests
{
    void CommandsRequest();
    void StateRequest();
    void Cash(int amount);
    void Coins(int amount);
    void Stats(int str, int sta, int dex, int intel, int magic, int statPoints, int loyalty);
    void GiveItem(int itemId, int count);
    void SetClass(int classId);
    void Zone(int zoneId);
    void SetLevel(int level, bool reset);
    void SetSkill(int pool, IReadOnlyList<int> trees, bool reset);
    void SetLook(int nation, int race);
    void SpawnRowRequest(int row);
    void SpawnEdit(bool persist, int row, int x, int z, int direction, int count, int respawnSeconds, int range);
    void Find(int kind, string query);
    void PlayerSheetRequest(int characterId);
    void InventoryEdit(int characterId, AdminInventoryEdit action, int position, int itemId, int count);
    void Go(int zone, int x, int z);
    void CollectionRacesRequest();
    void CollectionRaceStart(int raceId);
    void CollectionRaceClose(int raceId);
    void MonsterSheet(AdminMonsterBy by, int id);
    void MonsterEdit(bool persist, bool isMonster, int protoId, IReadOnlyList<(int Field, int Value)> values);
    void DropsEdit(bool persist, bool isMonster, int protoId, int dropRow, IReadOnlyList<(int Value, int Percent)> slots);
    void ItemGroupRequest(int group);
    void ItemGroupsRequest(string query);
    void ItemGroupEdit(bool persist, int group, IReadOnlyList<int> items);
    void DropSimulate(bool isMonster, int protoId, int rolls, int premiumBonus, int dropEventPercent, int coinEventPercent);
    void TargetSpawnRow(int runtimeId);
    void EventSchedulesRequest();
    void EventRuleSave(LibreKO.Domain.EventKind kind, int owner, LibreKO.Domain.EventRule rule);
    void EventRuleDelete(LibreKO.Domain.EventKind kind, int owner, int ruleId);
    void EventDurationSave(LibreKO.Domain.EventKind kind, int owner, int minutes);
    void LotteryRequest(int id);
    void LotterySave(AdminLottery lottery);
    void LotteryDelete(int id);
    void RaceRequest(int id);
    void RaceSave(AdminRace race);
    void RaceDelete(int id);
    void TipsRequest();
    void TipSave(AdminTip tip);
    void TipDelete(int id);
    void TipSettings(bool enabled, int intervalMinutes);
}

public sealed class AdminRequests : IAdminRequests
{
    private readonly IPacketSink sink;

    public AdminRequests(IPacketSink sink) => this.sink = sink;

    public void CommandsRequest()
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Commands);
        sink.Send(p);
    }

    public void StateRequest() => SendAdminByte(AdminRequestSubOpcode.State);

    public void Cash(int amount)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Cash);
        p.WriteInt(amount);
        sink.Send(p);
    }

    public void Coins(int amount)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Coins);
        p.WriteInt(amount);
        sink.Send(p);
    }

    public void Stats(int str, int sta, int dex, int intel, int magic, int statPoints, int loyalty)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Stats);
        p.WriteByte(ClampStatByte(str));
        p.WriteByte(ClampStatByte(sta));
        p.WriteByte(ClampStatByte(dex));
        p.WriteByte(ClampStatByte(intel));
        p.WriteByte(ClampStatByte(magic));
        p.WriteShort(WireNumbers.ClampToShort(Math.Max(0, statPoints)));
        p.WriteInt(Math.Max(0, loyalty));
        sink.Send(p);
    }

    public void GiveItem(int itemId, int count)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.GiveItem);
        p.WriteInt(itemId);
        p.WriteShort(WireNumbers.ClampToShort(Math.Clamp(count, 1, 9999)));
        sink.Send(p);
    }

    public void SetClass(int classId)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.SetClass);
        p.WriteShort(WireNumbers.ClampToShort(classId));
        sink.Send(p);
    }

    public void Zone(int zoneId)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Zone);
        p.WriteShort(WireNumbers.ClampToShort(zoneId));
        sink.Send(p);
    }

    public void SetLevel(int level, bool reset)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.SetLevel);
        p.WriteByte((byte)Math.Clamp(level, CharacterSheet.MinLevel, CharacterSheet.MaxLevel));
        p.WriteByte((byte)(reset ? AdminProgressMode.Reset : AdminProgressMode.Keep));
        sink.Send(p);
    }

    public void SetSkill(int pool, IReadOnlyList<int> trees, bool reset)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.SetSkill);
        p.WriteByte(ClampByte(pool));
        for (int tree = MasteryPoints.FirstTree; tree <= MasteryPoints.LastTree; tree++)
        {
            int index = tree - MasteryPoints.FirstTree;
            p.WriteByte(ClampByte(index < trees.Count ? trees[index] : 0));
        }
        p.WriteByte((byte)(reset ? AdminProgressMode.Reset : AdminProgressMode.Keep));
        sink.Send(p);
    }

    public void SetLook(int nation, int race)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.SetLook);
        p.WriteByte(ClampByte(nation));
        p.WriteByte(ClampByte(race));
        sink.Send(p);
    }

    public void SpawnRowRequest(int row) => SendAdminInt(AdminRequestSubOpcode.SpawnRow, row);

    public void SpawnEdit(bool persist, int row, int x, int z, int direction, int count, int respawnSeconds, int range)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)(persist ? AdminRequestSubOpcode.SpawnPersist : AdminRequestSubOpcode.SpawnSet));
        p.WriteInt(row);
        p.WriteInt(x);
        p.WriteInt(z);
        p.WriteInt(direction);
        p.WriteByte((byte)Math.Clamp(count, 0, byte.MaxValue));
        p.WriteShort((short)Math.Clamp(respawnSeconds, 0, short.MaxValue));
        p.WriteShort((short)Math.Clamp(range, 0, short.MaxValue));
        sink.Send(p);
    }

    public void Find(int kind, string query)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Find);
        p.WriteByte((byte)kind);
        p.WriteUtf8String(query);
        sink.Send(p);
    }

    public void PlayerSheetRequest(int characterId) => SendAdminInt(AdminRequestSubOpcode.PlayerSheet, characterId);

    public void InventoryEdit(int characterId, AdminInventoryEdit action, int position, int itemId, int count)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.InventoryEdit);
        p.WriteInt(characterId);
        p.WriteByte((byte)action);
        p.WriteByte((byte)Math.Clamp(position, 0, byte.MaxValue));
        p.WriteInt(itemId);
        p.WriteUShort((ushort)Math.Clamp(count, 0, ushort.MaxValue));
        sink.Send(p);
    }

    public void Go(int zone, int x, int z)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Go);
        p.WriteByte((byte)zone);
        p.WriteUShort((ushort)Math.Clamp(x, 0, ushort.MaxValue));
        p.WriteUShort((ushort)Math.Clamp(z, 0, ushort.MaxValue));
        sink.Send(p);
    }

    public void CollectionRacesRequest() => SendAdminByte(AdminRequestSubOpcode.CollectionRaces);

    public void CollectionRaceStart(int raceId) => SendAdminInt(AdminRequestSubOpcode.CollectionRaceStart, raceId);

    public void CollectionRaceClose(int raceId) => SendAdminInt(AdminRequestSubOpcode.CollectionRaceClose, raceId);

    public void MonsterSheet(AdminMonsterBy by, int id)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.Monster);
        p.WriteByte((byte)by);
        p.WriteInt(id);
        sink.Send(p);
    }

    public void MonsterEdit(bool persist, bool isMonster, int protoId, IReadOnlyList<(int Field, int Value)> values)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)(persist ? AdminRequestSubOpcode.MonsterPersist : AdminRequestSubOpcode.MonsterSet));
        p.WriteByte(isMonster ? (byte)1 : (byte)0);
        p.WriteInt(protoId);
        p.WriteByte(ClampByte(values.Count));
        foreach (var (field, value) in values)
        {
            p.WriteByte(ClampByte(field));
            p.WriteInt(value);
        }
        sink.Send(p);
    }

    public void DropsEdit(bool persist, bool isMonster, int protoId, int dropRow, IReadOnlyList<(int Value, int Percent)> slots)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)(persist ? AdminRequestSubOpcode.DropsPersist : AdminRequestSubOpcode.DropsSet));
        p.WriteByte(isMonster ? (byte)1 : (byte)0);
        p.WriteInt(protoId);
        p.WriteShort(WireNumbers.ClampToShort(dropRow));
        foreach (var (value, percent) in slots)
        {
            p.WriteInt(value);
            p.WriteShort(WireNumbers.ClampToShort(percent));
        }
        sink.Send(p);
    }

    public void ItemGroupRequest(int group) => SendAdminInt(AdminRequestSubOpcode.ItemGroup, group);

    public void ItemGroupsRequest(string query)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.ItemGroups);
        p.WriteUtf8String(query);
        sink.Send(p);
    }

    public void ItemGroupEdit(bool persist, int group, IReadOnlyList<int> items)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)(persist ? AdminRequestSubOpcode.ItemGroupPersist : AdminRequestSubOpcode.ItemGroupSet));
        p.WriteInt(group);
        p.WriteUShort((ushort)Math.Min(items.Count, ushort.MaxValue));
        foreach (int item in items)
            p.WriteInt(item);
        sink.Send(p);
    }

    public void DropSimulate(bool isMonster, int protoId, int rolls, int premiumBonus, int dropEventPercent, int coinEventPercent)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.DropSimulate);
        p.WriteByte(isMonster ? (byte)1 : (byte)0);
        p.WriteInt(protoId);
        p.WriteInt(rolls);
        p.WriteByte(ClampByte(premiumBonus));
        p.WriteByte(ClampByte(dropEventPercent));
        p.WriteByte(ClampByte(coinEventPercent));
        sink.Send(p);
    }

    public void TargetSpawnRow(int runtimeId) => SendAdminInt(AdminRequestSubOpcode.TargetSpawnRow, runtimeId);

    public void EventSchedulesRequest() => SendAdminByte(AdminRequestSubOpcode.EventSchedules);

    public void TipsRequest() => SendAdminByte(AdminRequestSubOpcode.Tips);

    public void TipSave(AdminTip tip)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.TipSave);
        p.WriteInt(tip.Id);
        p.WriteByte((byte)tip.Language);
        p.WriteByte((byte)(tip.Enabled ? 1 : 0));
        p.WriteString(tip.Text);
        sink.Send(p);
    }

    public void TipDelete(int id) => SendAdminInt(AdminRequestSubOpcode.TipDelete, id);

    public void TipSettings(bool enabled, int intervalMinutes)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.TipSettings);
        p.WriteByte((byte)(enabled ? 1 : 0));
        p.WriteUShort((ushort)intervalMinutes);
        sink.Send(p);
    }

    public void EventRuleSave(LibreKO.Domain.EventKind kind, int owner, LibreKO.Domain.EventRule rule)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.EventRuleSave);
        p.WriteByte((byte)kind);
        p.WriteInt(owner);
        p.WriteInt(rule.Id);
        p.WriteByte((byte)rule.Repeat);
        p.WriteInt(rule.Date is { } date ? LibreKO.Domain.EventCalendar.DateNumber(date) : 0);
        p.WriteByte(rule.Day is { } day ? (byte)day : byte.MaxValue);
        p.WriteByte(ClampByte(rule.DayOfMonth));
        p.WriteByte(ClampByte(rule.Hour));
        p.WriteByte(ClampByte(rule.Minute));
        p.WriteByte(ClampByte(rule.MinLevel));
        p.WriteByte(ClampByte(rule.MaxLevel));
        p.WriteByte(ClampByte(rule.CountdownMinutes));
        sink.Send(p);
    }

    public void EventDurationSave(LibreKO.Domain.EventKind kind, int owner, int minutes)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.EventDurationSave);
        p.WriteByte((byte)kind);
        p.WriteInt(owner);
        p.WriteUShort((ushort)Math.Clamp(minutes, 0, ushort.MaxValue));
        sink.Send(p);
    }

    public void EventRuleDelete(LibreKO.Domain.EventKind kind, int owner, int ruleId)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.EventRuleDelete);
        p.WriteByte((byte)kind);
        p.WriteInt(owner);
        p.WriteInt(ruleId);
        sink.Send(p);
    }

    public void LotteryRequest(int id) => SendAdminInt(AdminRequestSubOpcode.Lottery, id);

    public void LotterySave(AdminLottery lottery)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.LotterySave);
        p.WriteInt(lottery.Id);
        p.WriteSByteString(lottery.Name);
        p.WriteInt(lottery.DurationMinutes);
        p.WriteInt(lottery.UserLimit);
        p.WriteInt(lottery.ReqItemId);
        p.WriteInt(lottery.ReqItemCount);
        p.WriteByte(lottery.AutoStart ? (byte)1 : (byte)0);
        p.WriteByte(ClampByte(lottery.Rewards.Count));
        foreach (var reward in lottery.Rewards)
        {
            p.WriteInt(reward.Place);
            p.WriteInt(reward.ItemId);
            p.WriteInt(reward.Count);
        }
        sink.Send(p);
    }

    public void LotteryDelete(int id) => SendAdminInt(AdminRequestSubOpcode.LotteryDelete, id);

    public void RaceRequest(int id) => SendAdminInt(AdminRequestSubOpcode.Race, id);

    public void RaceSave(AdminRace race)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminRequestSubOpcode.RaceSave);
        p.WriteInt(race.Id);
        p.WriteSByteString(race.Name);
        p.WriteByte(ClampByte(race.ZoneId));
        p.WriteByte(ClampByte(race.MinLevel));
        p.WriteByte(ClampByte(race.MaxLevel));
        p.WriteInt(race.DurationMinutes);
        p.WriteInt(race.MaxWinners);
        p.WriteByte(race.AutoStart ? (byte)1 : (byte)0);
        p.WriteByte(ClampByte(race.Objectives.Count));
        foreach (var objective in race.Objectives)
        {
            p.WriteByte((byte)objective.Kind);
            p.WriteInt(objective.TargetId);
            p.WriteInt(objective.Count);
        }
        p.WriteByte(ClampByte(race.Rewards.Count));
        foreach (var reward in race.Rewards)
        {
            p.WriteInt(reward.ItemId);
            p.WriteInt(reward.Count);
            p.WriteByte(ClampByte(reward.Rate));
        }
        sink.Send(p);
    }

    public void RaceDelete(int id) => SendAdminInt(AdminRequestSubOpcode.RaceDelete, id);

    private void SendAdminInt(AdminRequestSubOpcode sub, int value)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)sub);
        p.WriteInt(value);
        sink.Send(p);
    }

    private void SendAdminByte(AdminRequestSubOpcode sub)
    {
        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)sub);
        sink.Send(p);
    }

    private static byte ClampStatByte(int value) => (byte)Math.Clamp(value, 1, 255);

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
}
