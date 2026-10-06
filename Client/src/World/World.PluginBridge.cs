using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private PluginGameBridge? _pluginBridge;
    private readonly List<GameLogLine> _pluginLog = new();

    private const int PluginHudLayer = 64;
    private const int PluginLogMax = 200;
    private static readonly Color PluginSystemLineColor = new("#9fd3ff");

    private const string ClanWindowId = "clan";

    private static readonly Dictionary<string, string> MainWindowKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["character_info"] = "Character",
        ["inventory"] = "Inventory",
        ["skills"] = "Skills",
        ["quests"] = "Quests",
        ["party"] = "Party",
    };

    private void PluginBridgeInit()
    {
        if (PluginHost.Ui.ExtraHud.Count > 0)
        {
            var layer = new CanvasLayer { Layer = PluginHudLayer };
            AddChild(layer);
            foreach (var build in PluginHost.Ui.ExtraHud)
                layer.AddChild(build());
        }
        _pluginBridge = new PluginGameBridge(this);
        _pluginBridge.Subscribe();
        PluginHost.Game.Attach(_pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge,
            _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge);
    }

    private void PluginNotifySkills()
    {
        if (_pluginBridge != null) Callable.From(PluginHost.Game.RaiseSkills).CallDeferred();
    }

    private void PluginLogAdd(GameLogKind kind, string text, Color color)
    {
        var line = new GameLogLine(kind, text, color);
        _pluginLog.Add(line);
        if (_pluginLog.Count > PluginLogMax) _pluginLog.RemoveAt(0);
        if (_pluginBridge != null) PluginHost.Game.RaiseLogLine(line);
    }

    private void PluginNotifyQuests()
    {
        if (_pluginBridge != null) Callable.From(PluginHost.Game.RaiseQuests).CallDeferred();
    }

    private void PluginBridgeDispose()
    {
        PluginHost.Game.Detach();
        _pluginBridge?.Unsubscribe();
        _pluginBridge = null;
    }

    private void PluginNotifyInventory() => _pluginBridge?.RaiseInventory();

    private void PluginNotifyTarget() => _pluginBridge?.RefreshTarget();

    private void PluginNotifyHotbar() => _pluginBridge?.RaiseHotbar();

    private void PluginNotifyMap() => _pluginBridge?.RaiseMap();

    private sealed class PluginGameBridge : IGameCharacter, IGameInventory, IGameTarget, IGameWindows, IGameChat,
        IGameHotbar, IGameMap, IGameCommands, IGameLog, IGameQuests, IGameSkills
    {
        public IGameCharacterPanel CharacterPanel => _w._characterPanelBridge ??= new CharacterPanelBridge(_w);
        public GameNpcPortrait? NpcPortrait => _w.DialogNpcPortrait();
        private const string SelectPlayerNotice = "Select a player to trade with.";

        public void GoTown() => _w.TownRecallPress();

        public void ToggleSit() => _w.ToggleSitting();

        public bool Running => _w._running;
        public bool Sitting => _w._selfSitting;
        public bool AutoAttacking => _w._autoAttack;
        public void ToggleRun() => _w.ToggleRunMode();
        public void ToggleAttack() => _w.ToggleAutoAttack();
        public void TurnCamera() => _w.StartCameraHalfTurn();
        public void OpenGameMenu() => _w.ToggleEsc(true);

        public void TradeWithTarget()
        {
            if (!IsPlayer)
            {
                _w.CombatNotice(SelectPlayerNotice);
                return;
            }
            _w.BeginTradeRequest(_targetId, _targetName);
        }

        private readonly World _w;
        private int _targetId = -1, _targetHp, _targetMaxHp;
        private string _targetName = "";
        private string _mapStem = "";
        private Texture2D? _mapTexture;

        public PluginGameBridge(World w) => _w = w;

        event Action? IGameCharacter.Changed { add { } remove { } }
        event Action? IGameInventory.Changed { add { } remove { } }
        event Action? IGameTarget.Changed { add { } remove { } }
        event Action? IGameHotbar.Changed { add { } remove { } }
        event Action? IGameMap.Changed { add { } remove { } }
        event Action<string>? IGameChat.LineAdded { add { } remove { } }
        event Action? IGameChat.InputRequested { add { } remove { } }
        event Action<GameLogLine>? IGameLog.LineAdded { add { } remove { } }
        event Action? IGameQuests.Changed { add { } remove { } }
        event Action? IGameSkills.Changed { add { } remove { } }

        public IReadOnlyList<GameSkillTab> Tabs =>
            SkillData.Pages(_w._selfClass, _w.SelfTransformModel()).Select(p => new GameSkillTab(p.Category, p.Label)).ToList();

        public int MasteryPool => _w.Mastery.Pool;

        public IReadOnlyList<GameMasteryTree> Trees
        {
            get
            {
                var list = new List<GameMasteryTree>();
                for (int type = MasteryPoints.FirstTree; type <= MasteryPoints.LastTree; type++)
                {
                    bool shown = MasteryPoints.ClassHasTree(_w._selfClass, type);
                    list.Add(new GameMasteryTree(type, SkillData.PageName(_w._selfClass, type), _w.Mastery.InTree(type),
                        shown, _w.CanSpendMastery(type), _w.MasteryHint(type)));
                }
                return list;
            }
        }

        public IReadOnlyList<GameSkill> Skills(int category)
        {
            var list = new List<GameSkill>();
            foreach (var page in SkillData.Pages(_w._selfClass, _w.SelfTransformModel()))
            {
                if (page.Category != category) continue;
                foreach (var s in page.Skills)
                {
                    bool met = _w.SkillRequirementMet(s);
                    var icon = met ? SkillData.Icon(s.Id) ?? SkillData.EnigmaIcon() : SkillData.EnigmaIcon();
                    list.Add(new GameSkill(s.Id, s.Name, icon, met, SkillTooltip(s)));
                }
            }
            return list;
        }

        public GameSkillInfo Info(int skillId)
        {
            if (SkillData.Get(skillId) is not { } s) return default;
            return new GameSkillInfo(s.Desc.Replace('|', '\n'), s.Msp, s.Level, s.Level, SkillData.MasteryType(s.Tree) > 0,
                SkillData.EquippedWeaponRequirementName(s.ItemGroup),
                s.NeedItem != 0 ? ItemData.DisplayName(s.NeedItem) : "",
                s.ConsumedItem != 0 && s.ConsumedItem != s.NeedItem ? ItemData.DisplayName(s.ConsumedItem) : "");
        }

        public void SpendMastery(int type) => _w.OnMasterySpend(type);

        public void AddToHotbar(int skillId) => _w.AddToHotbar(skillId);

        public void Subscribe()
        {
            var net = Net.I;
            net.SelfHpEvent += OnHp;
            net.SelfMpEvent += OnMp;
            net.PointChangeEvent += OnPoints;
            net.GoldChangeEvent += OnGold;
            net.ExpChangeEvent += OnExp;
            net.ItemStatsEvent += OnStats;
            net.NoticeEvent += OnNotice;
        }

        public void Unsubscribe()
        {
            var net = Net.I;
            net.SelfHpEvent -= OnHp;
            net.SelfMpEvent -= OnMp;
            net.PointChangeEvent -= OnPoints;
            net.GoldChangeEvent -= OnGold;
            net.ExpChangeEvent -= OnExp;
            net.ItemStatsEvent -= OnStats;
            net.NoticeEvent -= OnNotice;
        }

        private void OnNotice(string text) => _w.PluginLogAdd(GameLogKind.System, text, PluginSystemLineColor);

        public IReadOnlyList<GameLogLine> History => _w._pluginLog;

        public IReadOnlyList<GameQuestTrack> Tracked => _w.TrackedQuestsForPlugins();

        private void OnHp(int a, int b, int c) => RaiseCharacter();
        private void OnMp(int a, int b) => RaiseCharacter();
        private void OnPoints(int a, int b, int c, int d, int e, int f) => RaiseCharacter();
        private void OnGold(int a) { RaiseCharacter(); RaiseInventory(); }
        private void OnExp(long a) => RaiseCharacter();
        private void OnStats(DerivedStats s) => RaiseCharacter();

        public void RaiseCharacter() => Callable.From(PluginHost.Game.RaiseCharacter).CallDeferred();

        public void RaiseInventory() => Callable.From(PluginHost.Game.RaiseInventory).CallDeferred();

        public void RaiseHotbar() => Callable.From(PluginHost.Game.RaiseHotbar).CallDeferred();

        public void RaiseMap()
        {
            PluginHost.Game.RaiseMap();
        }

        public void RefreshTarget()
        {
            int id = _w._selectedId >= 0 && _w._ents.ContainsKey(_w._selectedId) ? _w._selectedId : -1;
            int hp = 0, max = 0;
            string name = "";
            if (id >= 0 && _w._ents.TryGetValue(id, out var e)) { hp = e.Hp; max = e.MaxHp; name = e.Name; }
            if (id == _targetId && hp == _targetHp && max == _targetMaxHp && name == _targetName) return;
            _targetId = id; _targetHp = hp; _targetMaxHp = max; _targetName = name;
            PluginHost.Game.RaiseTarget();
        }

        public string Name => Net.I.LastEnter.Name;
        public int Class => _w._selfClass;
        public string ClassName => World.ClassName(_w._selfClass);
        public int Race => _w._selfRace;
        public int Nation => Net.I.LastEnter.Nation;
        public string NationName => World.NationName(Net.I.LastEnter.Nation);
        public int Level => _w.Sheet.Level;
        public int Hp => _w.Vitals.Hp;
        public int MaxHp => _w.Vitals.MaxHp;
        public int Mp => _w.Vitals.Mp;
        public int MaxMp => _w.Vitals.MaxMp;
        public long Exp => _w.Sheet.Exp;
        public long MaxExp => _w.Sheet.MaxExp;
        public double ExpPercent => _w.Sheet.ExpPercent;
        public int Gold => _w.Sheet.Gold;
        public int KnightCash => _w.Sheet.KnightCash;
        public int Weight => _w.CarriedWeight();
        public int MaxWeight => _w.Sheet.MaxWeight;
        public int Str => _w.Sheet.Str;
        public int Sta => _w.Sheet.Sta;
        public int Dex => _w.Sheet.Dex;
        public int Intel => _w.Sheet.Intel;
        public int Mag => _w.Sheet.Mag;
        public int Points => _w.Sheet.Points;
        public int Ap => _w.Sheet.Ap;
        public int Ac => _w.Sheet.Ac;
        public int Np => _w.Sheet.Np;
        public int Resist(int index) => _w.Sheet.ResistAt(index);
        public void AllocateStat(int statRow) => _w.OnAllocate(statRow);

        public int Length => _w.Inv.Length;
        public int GridStart => World.GridStart;
        public int GridCount => World.GridCount;

        public GameItem At(int slot)
        {
            if (slot < 0 || slot >= _w.Inv.Length) return GameItem.Empty(slot);
            var it = _w.Inv[slot];
            if (it.IsEmpty) return GameItem.Empty(slot);
            var def = ItemData.Get(it.ItemId);
            return new GameItem(slot, it.ItemId, ItemData.DisplayName(it.ItemId), ItemData.ShownCount(def, it),
                it.Durability, def?.Grade ?? 0, def?.Kind ?? 0, ItemData.Icon(it.ItemId),
                def != null && Inventory.IsTwoHandedSlotType(def.Slot));
        }

        public void Move(int from, int to) => _w.MoveBetween(from, to);
        public void MoveAmount(int from, int to, int count) => _w.MoveBetween(from, to, count);
        public int TransferToInventorySlot(int from) => from >= 0 && from < _w.Inv.Length
            ? _w.Inv.FirstStackOrFreeGridSlot(_w.Inv[from], ItemData.Get(_w.Inv[from].ItemId)?.Countable ?? 0) : -1;
        public void ConfirmDrop(int slot, int itemId) => _w.DestroyInventoryItem(slot, itemId);

        public void Use(int slot) => _w.InventoryContext(slot);

        public void Drop(int slot) => _w.AskDeleteItem(slot);

        public void Arrange() => Net.I.SendInventoryArrange();

        void IGameInventory.ShowTooltip(int slot)
        {
            if (slot < 0 || slot >= _w.Inv.Length || _w.Inv[slot].IsEmpty) return;
            _w.ShowItemTooltip(slot, _w.Inv[slot]);
        }

        void IGameInventory.HideTooltip() => _w.HideItemTooltip();

        public bool Has => _targetId >= 0;
        public int Id => _targetId;
        string IGameTarget.Name => _targetName;
        int IGameTarget.Level => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) ? e.Level : 0;
        int IGameTarget.Hp => _targetHp;
        int IGameTarget.MaxHp => _targetMaxHp;
        public bool Hostile => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && e.Attackable;
        public bool IsPlayer => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && !e.IsNpc;

        public void Clear() => _w.Deselect();

        public int NotificationCount(string id) => id.ToLowerInvariant() switch
        {
            "mail" => Net.I.MailUnread,
            "achievements" => _w._achClaimablePerTab.Values.Sum(),
            "attendance" => _w.ClaimableAttendanceCount(),
            _ => 0,
        };

        private bool SetServiceWindow(string id, bool? shown)
        {
            switch (id.ToLowerInvariant())
            {
                case "mail":
                    if (shown == null || shown != _w._mailShown) _w.ToggleMail();
                    return true;
                case "achievements":
                    if (shown == null || shown != _w._achShown) _w.ToggleAchievements();
                    return true;
                case "attendance":
                    if (shown == null || shown != _w._attendanceShown) _w.ToggleAttendance();
                    return true;
                case "shoppingmall":
                    if (shown == null || shown != _w._pusShown) _w.ToggleShoppingMall();
                    return true;
                default: return false;
            }
        }

        public bool IsOpen(string id)
        {
            switch (id.ToLowerInvariant())
            {
                case "mail": return _w._mailShown;
                case "achievements": return _w._achShown;
                case "attendance": return _w._attendanceShown;
                case "shoppingmall": return _w._pusShown;
            }
            if (string.Equals(id, "zonemap", StringComparison.OrdinalIgnoreCase)) return _w._fullMapShown;
            if (MainWindowKeys.TryGetValue(id, out var key)) return _w.MainWindowOpen(key);
            return HudWindow.Find(id)?.Visible ?? false;
        }

        public void Open(string id)
        {
            if (SetServiceWindow(id, true)) return;
            if (string.Equals(id, "zonemap", StringComparison.OrdinalIgnoreCase))
            {
                if (!_w._fullMapShown) _w.ToggleFullMap();
                return;
            }
            if (string.Equals(id, "genie", StringComparison.OrdinalIgnoreCase))
            {
                if (!_w._genieShown) _w.ToggleGenie();
                return;
            }
            if (MainWindowKeys.TryGetValue(id, out var key)) { _w.ShowMainWindow(key); return; }
            if (HudWindow.Find(id) is { } win) win.Visible = true;
        }

        public void Close(string id)
        {
            if (SetServiceWindow(id, false)) return;
            if (string.Equals(id, "zonemap", StringComparison.OrdinalIgnoreCase))
            {
                if (_w._fullMapShown) _w.ToggleFullMap();
                return;
            }
            if (string.Equals(id, "genie", StringComparison.OrdinalIgnoreCase)) { _w.CloseGenie(); return; }
            if (MainWindowKeys.TryGetValue(id, out var key))
            {
                if (_w.MainWindowOpen(key)) _w.ToggleMainWindow(key);
                return;
            }
            if (HudWindow.Find(id) is { } win) win.Visible = false;
        }

        public void Toggle(string id)
        {
            if (SetServiceWindow(id, null)) return;
            if (string.Equals(id, "zonemap", StringComparison.OrdinalIgnoreCase)) { _w.ToggleFullMap(); return; }
            if (string.Equals(id, "genie", StringComparison.OrdinalIgnoreCase)) { _w.ToggleGenie(); return; }
            if (string.Equals(id, ClanWindowId, StringComparison.OrdinalIgnoreCase)) { _w.OpenCharacterPage(CharacterPage.Clan); return; }
            if (MainWindowKeys.TryGetValue(id, out var key)) { _w.ToggleMainWindow(key); return; }
            if (HudWindow.Find(id) is { } win) win.Visible = !win.Visible;
        }

        public IReadOnlyList<string> ReadHistory(int mask,bool timestamps,string colors)=>_w.Chat?.ClassicHistory(mask,timestamps,colors)??Array.Empty<string>();
        public bool LinkInventoryItem(int slot)
        {
            if(_w.Chat?.PluginTyping!=true || !Input.IsKeyPressed(Key.Shift) || slot<0 || slot>=_w.Inv.Length || _w.Inv[slot].IsEmpty) return false;
            int id=_w.Inv[slot].ItemId;
            if(id==LibreKO.Domain.ChatItemLink.RefusedItem) return false;
            PluginHost.Game.RaiseChatItemLink(id,ItemData.DisplayName(id));
            return true;
        }
        public void ShowLinkTooltip(int itemId)=>_w.ShowChatItemTip(itemId);
        public void HideLinkTooltip()=>_w.HideItemTooltip();
        public void PlayerMenu(string name,Vector2 at)=>_w.ShowPlayerMenuByName(name,at);
        public IReadOnlyList<LibreKO.Domain.NearbyRow> NearbyPlayers()=>_w.ClassicNearbyPlayers();
        IReadOnlyList<string> IGameChat.History => _w.Chat?.History ?? Array.Empty<string>();

        public void Send(string text) => _w.Chat?.SendText(text);

        public void SetTyping(bool typing)
        {
            if (_w.Chat != null) _w.Chat.PluginTyping = typing;
        }

        public int Pages => HotbarLayout.Pages;
        public int SlotsPerPage => HotbarLayout.SlotsPerPage;
        public int Page => _w._hotPage;
        public bool Locked => Config.HotbarLocked;
        public int SelectedAbs => _w._hotSelected;

        public HotSlotInfo Slot(int abs)
        {
            if (abs < 0 || abs >= HotbarLayout.Total) return HotSlotInfo.Empty(abs);
            int id = _w._hotbar[abs];
            if (id == 0) return HotSlotInfo.Empty(abs);
            var s = SkillData.Get(id);
            if (s == null && ItemData.Get(id) is { Effect1: not 0 } def) s = SkillData.Get(def.Effect1);
            float cooldown = s == null ? 0f : _w.SkillCooldown(s, Now());
            bool isSkill = SkillData.IsSkill(id);
            int needId = isSkill ? s?.ConsumedItem ?? 0 : id;
            int count = -1;
            bool enough = true;
            if (needId != 0 && ItemData.Get(needId) != null)
            {
                int need = s is { IsRanged: true } ? Mathf.Max(1, s.NeedArrow) : 1;
                count = _w.CountInBackpack(needId);
                enough = count >= need;
            }
            return new HotSlotInfo(abs, id, isSkill,
                isSkill ? SkillData.Icon(id) : ItemData.Icon(id), cooldown,
                isSkill ? s?.Name ?? "" : ItemData.DisplayName(id),
                isSkill && s != null ? SkillTooltip(s) : "", count, enough);
        }

        public void Activate(int slotInPage) => _w.ActivateHotSlot(slotInPage);

        public void ActivateAbs(int abs) => _w.FireHotSlot(abs);
        public void Select(int abs) => _w.SelectHotAbs(abs);

        public void SetPage(int page) => _w.SetHotPage(page);

        public void ChangePage(int delta) => _w.ChangeHotPage(delta);

        void IGameHotbar.Drop(int abs, int id, int fromAbs) => _w.DropOntoHotAbs(abs, id, fromAbs);

        void IGameHotbar.Clear(int abs) => _w.ClearHotAbs(abs);

        public void SetLocked(bool locked) => _w.ApplyHotbarLayout(locked, Config.HotbarVertical, Config.HotbarExtraBars);

        void IGameHotbar.ShowTooltip(int abs) => _w.HotAbsHover(abs, true);

        void IGameHotbar.HideTooltip() => _w.HideItemTooltip();

        public int Zone => _w._zone;
        public string ZoneName => MapName(_w._zone);
        public float X => _w._myKoX;
        public float Z => _w._myKoZ;
        public float HeadingDegrees => _w.CharacterMapHeading;
        public bool MiniMapVisible => _w._miniMapShown;
        public float WorldExtent => _w._miniMap?.WorldExtent ?? 0f;
        public IReadOnlyList<MiniMap.Blip> Blips => _w._blipScratch;

        public Texture2D? MapTexture
        {
            get
            {
                string stem = _w._terrain?.ZoneStem ?? ZoneCatalog.Stem(_w._zone) ?? "";
                if (stem != _mapStem)
                {
                    _mapStem = stem;
                    _mapTexture = stem.Length > 0 ? MiniMap.LoadBaked(stem) : null;
                }
                return _mapTexture;
            }
        }
    }
}
