using System.Globalization;
using Godot;

namespace LibreKO;

public partial class World
{
    private CharacterPanelBridge? _characterPanelBridge;
    private HudWindow? _characterClanDetails;
    private List<(string Name, int Points)> _characterDonations = new();

    private void OpenCharacterClanDetails()
    {
        if (_characterClanDetails == null)
        {
            _characterClanDetails = new HudWindow("character_clan_details", "Knights Management",
                new Vector2(370, 82), CharacterPageWidth) { Visible = false };
            var parent = _clanContent.GetParent();
            parent.RemoveChild(_clanContent);
            // Keep native navigation slots while sharing the complete live management view.
            var slot = new Control { Visible = false };
            parent.AddChild(slot);
            _characterPages[CharacterPage.Clan] = slot;
            _characterClanDetails.Body.AddChild(_clanContent);
            _mainLayer.AddChild(_characterClanDetails);
        }
        _clanContent.Visible = true;
        _characterClanDetails.Visible = true;
        ApplyMyClan();
        EnsureClanLoaded();
        RefreshClanTab();
    }

    private sealed class CharacterPanelBridge : IGameCharacterPanel
    {
        private readonly World _w;
        public CharacterPanelBridge(World world) => _w = world;
        public string SelectedPage => _w._selectedCharacterPage.ToString().ToLowerInvariant();
        public string RaceName => StarterStats.RaceName(_w._selfRace);
        public string JobName => CharacterClassCatalog.SpecializationName(_w._selfClass);
        public string LevelLabel => _w.Sheet.LevelLabel;
        public string TitleName => _w._stTitleBtn?.Text ?? "Title: none";
        public MyClanInfo Clan => _w.MyClan;
        public int QuestFilter => _w._questFilter;
        public int QuestKind => _w._questKind is { } kind ? (int)kind + 1 : 0;
        public IReadOnlyList<Window> Dialogs => new Window[] { _w._clanInviteAsk, _w._clanDisbandAsk,
            _w._clanLeaveAsk, _w._clanRemoveAsk, _w._clanAllianceAsk, _w._clanConfirmAsk, _w._clanMemberMenu };
        public int StatBonus(int row) => _w.Sheet.StatBonusAtRow(row);

        public void SelectPage(string page)
        {
            if (!Enum.TryParse<CharacterPage>(page, true, out var parsed) || !Enum.IsDefined(parsed)) return;
            _w.ShowCharacterPage(parsed);
        }

        public string Status(string section) => section switch
        {
            "friends" => _w._friendStatus?.Text ?? "",
            "clan" => _w._clanStatus?.Text ?? "",
            "union" => _w._allianceNotice ?? "",
            _ => "",
        };

        public IReadOnlyList<GamePanelRow> Rows(string section)
        {
            var rows = new List<GamePanelRow>();
            switch (section)
            {
                case "clan":
                    foreach (var m in _w._clanMembers.OrderByDescending(m => m.IsOnline).ThenBy(m => m.Fame)
                                 .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var job = CharacterClassCatalog.DisplayName(m.Class);
                        var seen = m.IsOnline ? "Online" : $"Offline: {m.HoursSinceLogin} hours";
                        rows.Add(new(m.Name, new[] { ClanRanks.Name(m.Fame), m.Name, m.Level.ToString(), job },
                            $"{m.Name}\n{ClanRanks.Name(m.Fame)} · Lv {m.Level} · {CharacterClassCatalog.SpecializationName(m.Class)}\n{seen}\n{m.Memo}",
                            m.IsOnline ? new Color("fff0c8") : new Color("b5b5b5"), m.IsOnline));
                    }
                    break;
                case "friends":
                    foreach (var f in _w._friends.OrderByDescending(f => f.Status).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                        rows.Add(new(f.Name, new[] { f.Name }, $"{f.Name}\n{FriendDetailLine(f)}",
                            f.InParty ? new Color("ff9292") : f.IsOnline ? new Color("8ff099") : new Color("bbbbbb"), f.IsOnline));
                    break;
                case "union":
                    foreach (var c in _w._allianceClans)
                    {
                        var officers = string.Join("\n", c.Officers.Select(o => $"{ClanRanks.Name(o.Fame)}: {o.Name}"));
                        rows.Add(new(c.Id.ToString(), new[] { c.Name, c.Id == Clan.AllianceId ? "Leader" : "Member" },
                            $"{c.Name}\n{officers}", c.Id == Clan.ClanId ? new Color("fff0a0") : Colors.White));
                    }
                    break;
                case "contribution":
                    foreach (var d in _w._characterDonations.OrderByDescending(d => d.Points))
                        rows.Add(new(d.Name, new[] { d.Name, d.Points.ToString("N0") }, $"{d.Name}: {d.Points:N0} Contribution", Colors.White));
                    break;
                case "quests":
                    var selfName = Net.I.LastEnter.Name ?? "";
                    foreach (var (id, state) in _w.QuestsInView())
                    {
                        var facts = _w.QuestFacts(id);
                        var title = _w.QuestName(id, selfName);
                        var status = StateName(state);
                        var shortState = state switch { 1 => "Active", 2 => "Done", 3 => "Ready", 4 => "Open", _ => "Open" };
                        var reset = "";
                        if (_w._questViews.TryGetValue(id, out var view) && view.Daily && view.NextReset > 0)
                            reset = $"\nDaily reset: {DateTimeOffset.FromUnixTimeSeconds(view.NextReset).ToLocalTime():g}";
                        bool active = state is QuestStateActive or QuestStateReadyToTurnIn;
                        bool abandon = active && view?.AutoAccepted != true;
                        bool claim = view != null ? view.CanClaim && view.Options.Length == 0 : state == QuestStateReadyToTurnIn;
                        bool tracked = _w._questTracked.Contains(id);
                        rows.Add(new(id.ToString(), new[] { title, shortState, "—" },
                            $"{title}\n{facts.Kind} · Lv {facts.Level} · {status}\n{_w.QuestJournal(id, selfName)}{reset}" + (tracked ? "\nTracked" : ""),
                            state == QuestStateReadyToTurnIn ? new Color("fff080") : Colors.White, true, active, abandon, claim, tracked));
                    }
                    break;
            }
            return rows;
        }

        public void Refresh(string section)
        {
            switch (section)
            {
                case "friends": _w.EnsureFriendsLoaded(); Net.I.SendFriendListRequest(); break;
                case "clan": if (Clan.InClan) Net.I.SendClanMembersRequest(); break;
                case "union": if (Clan.InClan) Net.I.SendAllianceList(); break;
                case "contribution": if (Clan.InClan) Net.I.SendClanDonationList(); break;
                case "quests": Net.I.SendQuestLogRequest(); break;
            }
        }

        public void Act(string action, string selection = "", string value = "")
        {
            bool member = _w._clanMembers.Any(m => string.Equals(m.Name, selection, StringComparison.OrdinalIgnoreCase));
            bool other = !string.Equals(selection, Net.I.LastEnter.Name, StringComparison.OrdinalIgnoreCase);
            switch (action)
            {
                case "titles": _w.ToggleTitlePicker(); return;
                case "presets": _w.TogglePreset(); return;
                case "clan_management": _w.OpenCharacterClanDetails(); return;
                case "clan_contribution":
                    if (Clan.InClan) { _w.ShowClanTab(ClanTab.Points); _w.OpenCharacterClanDetails(); }
                    return;
                case "clan_donate": if (Clan.InClan) _w.ToggleClanPoints(); return;
                case "clan_leave": if (Clan.InClan) _w.OnLeaveClan(); return;
                case "clan_invite":
                    if (Clan.CanInvite && _w._selectedId > 0 && _w._ents.TryGetValue(_w._selectedId, out var target) && !target.IsNpc)
                        Net.I.SendClanInvite(_w._selectedId);
                    else _w.CombatNotice("Select a player. A chief or vice-chief can invite members.");
                    return;
                case "clan_private": if (member && other) MemberAction(selection, 1); return;
                case "clan_party": if (member && other) MemberAction(selection, 2); return;
                case "clan_info": if (member) _w.RequestUserInformation(selection); return;
                case "clan_appoint": if (member && other && Clan.IsChief) MemberAction(selection, 4); return;
                case "clan_handover": if (member && other && Clan.IsChief) MemberAction(selection, 5); return;
                case "clan_remove":
                    if (member && other && Clan.IsChief)
                        _w.AskClanConfirm("Expel from clan", "Expel", $"Do you really want to expel {selection}?", () => Net.I.SendClanKick(selection));
                    return;
                case "union_leave": if (Clan.IsChief) _w.OnAllianceButton(); return;
                case "union_add":
                    if (!Clan.IsChief || Clan.Flag < ClanTypes.Promoted) return;
                    if (Clan.AllianceId == 0) { _w.OnAllianceButton(); return; }
                    if (Clan.AllianceId == Clan.ClanId && _w._selectedId > 0 && _w._ents.TryGetValue(_w._selectedId, out var ally) && !ally.IsNpc)
                        Net.I.SendAllianceInsert(_w._selectedId);
                    else _w.CombatNotice("Target the chief of the clan you want to ally with.");
                    return;
                case "union_remove":
                    if (Clan.IsChief && Clan.AllianceId == Clan.ClanId && int.TryParse(selection, out int clanId)
                        && clanId != Clan.ClanId && _w._allianceClans.Any(c => c.Id == clanId))
                        _w.AskClanConfirm("Expel from alliance", "Expel", "Expel the selected clan from the alliance?", () => Net.I.SendAlliancePunish(clanId));
                    return;
                case "clan_chat": _w.Chat.SetChannel(6); PluginHost.Game.RaiseChatChannelRequested("$"); _w.Chat.Open(); return;
                case "union_chat": _w.Chat.SetChannel(15); PluginHost.Game.RaiseChatChannelRequested("&"); _w.Chat.Open(); return;
                case "friend_add":
                    _w._friendAddInput.Text = value.Trim().Length > 0 ? value.Trim() : _w._selectedId > 0 && _w._ents.TryGetValue(_w._selectedId, out var f) && !f.IsNpc ? f.Name : "";
                    _w.DoFriendAdd(); return;
                case "friend_remove": if (_w._friends.Any(f => f.Name == selection)) Net.I.SendFriendRemove(selection); return;
                case "friend_private": if (_w._friends.Any(f => f.Name == selection && f.IsOnline)) _w.OpenWhisperWith(selection); return;
                case "friend_party": if (_w._friends.Any(f => f.Name == selection && f.IsOnline)) _w.InvitePlayerToParty(selection); return;
                case "quest_filter": if (int.TryParse(value, out int filter) && filter is >= 0 and <= 2) _w.SetQuestFilter(filter); return;
                case "quest_kind": if (int.TryParse(value, out int kind) && kind is >= 0 and <= 3) _w.SetQuestKind(kind == 0 ? null : (QuestData.Kind)(kind - 1)); return;
                case "quest_select": SelectQuest(selection); return;
                case "quest_details":
                    if (SelectQuest(selection)) _w.ShowMainWindow("Quests", refresh: false);
                    return;
                case "quest_track": if (SelectQuest(selection) && !_w._questTrackBtn.Disabled) _w.ToggleTrackSelectedQuest(); return;
                case "quest_abandon": if (SelectQuest(selection) && !_w._questAbandonBtn.Disabled) _w.AbandonSelectedQuest(); return;
                case "quest_complete": if (SelectQuest(selection) && !_w._questCompleteBtn.Disabled) _w.CompleteSelectedQuest(); return;
            }
        }

        private void MemberAction(string name, int action) { _w._ctxMember = name; _w.OnMemberMenuAction(action); }
        private bool SelectQuest(string selection)
        {
            if (!int.TryParse(selection, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                || !_w.QuestsInView().Any(q => q.QuestId == id)) return false;
            _w._questSelected = id;
            _w.RefreshQuestDetail();
            return true;
        }
    }
}
