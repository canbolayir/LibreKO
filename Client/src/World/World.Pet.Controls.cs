using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private readonly Label[] _petDetails = new Label[8];
    private readonly FamiliarSkillButton[] _petDetailSkills = new FamiliarSkillButton[PetSkills.SlotsPerPage];
    private Label _petDetailSkillPage = null!;
    private Button _petDetailPrevious = null!, _petDetailNext = null!;
    private List<int> _petDetailSkillIds = new();
    private int _petDetailPage;
    private int _petDetailClass = -1;
    private ServiceTabs _petPageTabs = null!;
    private readonly VBoxContainer[] _petPages = new VBoxContainer[3];

    private void BuildPetDetails(VBoxContainer root)
    {
        SkillData.EnsureLoaded();
        var oldChildren = root.GetChildren();
        _petPortrait = new FamiliarPortraitView { Name = "pet_portrait", CustomMinimumSize = new Vector2(80, 80), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(_petPortrait);
        _petPageTabs = new ServiceTabs { Name = "pet_pages" };
        _petPageTabs.SetTabs(new[] { "Ability", "Items", "Skills" }, 0); root.AddChild(_petPageTabs);
        for (int i = 0; i < _petPages.Length; i++)
        { _petPages[i] = new VBoxContainer { Name = "pet_page_" + i }; root.AddChild(_petPages[i]); }
        foreach (var bar in new[] { _petHpBar, _petMpBar, _petExpBar, _petSatBar }) bar.GetParent().Reparent(_petPages[0]);
        _petBagCells[0].GetParent().Reparent(_petPages[1]);
        _petFeedBtn.GetParent().Reparent(_petPages[1]);
        foreach (var node in oldChildren)
            if (node.GetParent() == root && node != _petNameLbl.GetParent() && node != _petAttackBtn.GetParent() && node != _petStatus)
            { root.RemoveChild(node); node.QueueFree(); }
        root.MoveChild(_petStatus, root.GetChildCount() - 1);
        _petPageTabs.Selected += page => { for (int i = 0; i < _petPages.Length; i++) _petPages[i].Visible = i == page; };
        for (int i = 0; i < _petPages.Length; i++) _petPages[i].Visible = i == 0;
        var stats = new GridContainer { Columns = 2, Name = "pet_details" };
        _petPages[0].AddChild(stats);
        string[] captions = { "Attack", "Defense", "Flame", "Glacier", "Lightning", "Magic", "Curse", "Poison" };
        for (int i = 0; i < captions.Length; i++)
        {
            var label = UiTheme.Text(captions[i] + "  —", 13, UiTheme.TextHi);
            label.Name = "pet_detail_" + i; stats.AddChild(label); _petDetails[i] = label;
        }
        var skills = new GridContainer { Columns = 4, Name = "pet_skills" }; _petPages[2].AddChild(skills);
        for (int i = 0; i < _petDetailSkills.Length; i++)
        {
            int index = i;
            var button = new FamiliarSkillButton { Name = "pet_skill_" + i, FocusMode = Control.FocusModeEnum.None };
            UiTheme.ActionButton("", "", button);
            button.CustomMinimumSize = new Vector2(44, 44);
            button.Pressed += () => {
                int skillIndex = _petDetailPage * PetSkills.SlotsPerPage + index;
                if (!_selfDead && skillIndex < _petDetailSkillIds.Count) UsePetSkill(_petDetailSkillIds[skillIndex]);
            };
            skills.AddChild(button); _petDetailSkills[i] = button;
        }
        var pages = new HBoxContainer(); _petPages[2].AddChild(pages);
        _petDetailPrevious = UiTheme.SmallButton("<", "Previous skills"); _petDetailPrevious.Name = "pet_skill_previous";
        _petDetailNext = UiTheme.SmallButton(">", "Next skills"); _petDetailNext.Name = "pet_skill_next";
        _petDetailSkillPage = UiTheme.Text("1/1", 13, UiTheme.TextHi); _petDetailSkillPage.Name = "pet_skill_page";
        _petDetailPrevious.Pressed += () => { _petDetailPage = Math.Max(0, _petDetailPage - 1); RefreshPetDetails(Network.Net.I.Pet); };
        _petDetailNext.Pressed += () => { _petDetailPage++; RefreshPetDetails(Network.Net.I.Pet); };
        pages.AddChild(_petDetailPrevious); pages.AddChild(_petDetailSkillPage); pages.AddChild(_petDetailNext);
    }

    private void RefreshPetDetails(PetSheet? pet)
    {
        if (_petDetails[0] == null) return;
        if (pet == null) _petPortrait.SetAppearance(null); else RefreshPetPortrait();
        string[] captions = { "Attack", "Defense", "Flame", "Glacier", "Lightning", "Magic", "Curse", "Poison" };
        for (int i = 0; i < _petDetails.Length; i++)
        {
            int value = pet == null ? 0 : i == 0 ? pet.Attack : i == 1 ? pet.Defence : pet.Resists[i - 2];
            _petDetails[i].Text = captions[i] + "  " + (pet == null ? "—" : value.ToString());
        }
        int petClass = pet?.Class ?? -1;
        if (_petDetailClass != petClass)
        {
            _petDetailClass = petClass; _petDetailPage = 0;
            var candidates = new List<(int, int)>();
            foreach (var skill in SkillData.All) candidates.Add((skill.Id, skill.Tree));
            _petDetailSkillIds = pet == null ? new List<int>() : PetSkills.BarSkills(candidates, petClass);
        }
        int pageCount = Math.Max(1, (_petDetailSkillIds.Count + PetSkills.SlotsPerPage - 1) / PetSkills.SlotsPerPage);
        _petDetailPage = Math.Clamp(_petDetailPage, 0, pageCount - 1);
        _petDetailSkillPage.Text = $"{_petDetailPage + 1}/{pageCount}";
        _petDetailPrevious.Disabled = _petDetailPage == 0;
        _petDetailNext.Disabled = _petDetailPage + 1 >= pageCount;
        for (int i = 0; i < _petDetailSkills.Length; i++)
        {
            int index = _petDetailPage * PetSkills.SlotsPerPage + i;
            var skill = index < _petDetailSkillIds.Count ? SkillData.Get(_petDetailSkillIds[index]) : null;
            bool locked = pet == null || skill == null || !PetSkills.Unlocked(skill.Level, pet.Level);
            var button = _petDetailSkills[i];
            button.SkillId = skill?.Id ?? 0;
            button.Icon = skill == null ? null : locked ? SkillData.EnigmaIcon() : SkillData.Icon(skill.Id);
            button.Disabled = _selfDead || locked || pet!.Mp < skill!.Msp;
            button.TooltipText = skill == null ? "" : SkillTooltip(skill) + (locked ? $"\nFamiliar level {skill.Level}" : "");
        }
        RefreshPetDetailCooldowns(Now());
    }

    private void RefreshPetDetailCooldowns(double now)
    {
        var pet = Network.Net.I.Pet;
        foreach (var button in _petDetailSkills)
        {
            if (button == null) continue;
            var skill = SkillData.Get(button.SkillId);
            float cooldown = button.SkillId == 0 ? 0 : PetCooldown(button.SkillId, now);
            button.SetCooldown(cooldown);
            button.Disabled = _selfDead || pet == null || skill == null
                || !PetSkills.Unlocked(skill.Level, pet.Level) || pet.Mp < skill.Msp || cooldown > 0;
        }
    }
}
