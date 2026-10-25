using Godot;

namespace LibreKO.Features.Hud;

internal sealed partial class PetSkillBarFeature : IFeature, ILateTickFeature
{
    private readonly IWorldContext context;
    private readonly HotbarFeature hotbar;
    private readonly CombatSession combat;
    private readonly PetSession pet;
    private readonly IPetRequests petRequests;
    private readonly SelfSession self;

    public PetSkillBarFeature(IWorldContext context, HotbarFeature hotbar, CombatSession combat, PetSession pet, IPetRequests petRequests, SelfSession self)
    {
        this.context = context;
        this.hotbar = hotbar;
        this.combat = combat;
        this.pet = pet;
        this.petRequests = petRequests;
        this.self = self;
    }

    public void LateTick(double delta, double nowSec) => PetBarTick(nowSec);

    public void Build()
    {
        PetBarLayer = new CanvasLayer { Layer = UiLayers.Hud };
        context.Root.AddChild(PetBarLayer);
        BuildPetBar();
        pet.PetSummonedEvent += OnPetBarSummoned;
        pet.PetGoneEvent += HidePetBar;
        pet.PetVitalsEvent += RefreshPetBar;
        pet.PetExpEvent += OnPetBarExp;
        combat.MagicEvent += OnPetSkillMagic;
        combat.AttackEvent += OnFamiliarSwing;
        if (pet.Pet is { } sheet) OnPetBarSummoned(sheet);
    }

    public void Dispose()
    {
        pet.PetSummonedEvent -= OnPetBarSummoned;
        pet.PetGoneEvent -= HidePetBar;
        pet.PetVitalsEvent -= RefreshPetBar;
        pet.PetExpEvent -= OnPetBarExp;
        combat.MagicEvent -= OnPetSkillMagic;
        combat.AttackEvent -= OnFamiliarSwing;
        petSkillGeneration++;
        petSkillCasts.Clear();
    }

    private const string PetBarLayoutId = "hud_pet_bar";
    private const string LayoutFileKind = "familiar_bar";
    private const int PetBarSlotSize = 40;
    private const int PetBarGap = 3;
    private const int TextPetNotSummoned = 14017;
    private const int TextNotPetSkill = 14018;
    private const string PetNoTargetText = "Select a monster for your familiar.";
    private const string PetNoManaText = "Your familiar does not have enough MP.";
    private const double AutoCastSpacingSeconds = 1.0;
    private const double FightMemorySeconds = 3.0;
    private const int NoTarget = -1;

    internal CanvasLayer PetBarLayer { get; set; } = null!;
    internal PanelContainer PetBar { get; private set; } = null!;
    private PetSkillCell petAttackCell = null!;
    private readonly List<PetSkillCell> petCells = new();
    private Button petPageBtn = null!;
    internal PetBarLayout Layout { get; private set; } = new();
    internal IReadOnlyList<int> FamiliarSkills { get; private set; } = new List<int>();
    private string layoutPath = "";
    internal int PetBarPage { get; set; }

    internal event Action? FamiliarSkillsChanged;

    internal Dictionary<int, double> PetSkillReadyAt { get; } = new();

    private readonly record struct PetSkillCast(int CasterId, int Serial);

    private readonly Dictionary<int, PetSkillCast> petSkillCasts = new();

    private int petSkillGeneration;
    private int petSkillCastSerial;
    private int fightTargetId = NoTarget;
    private double fightSeenAt;
    private double autoCastAt;

    private void OnFamiliarSwing(AttackReply reply)
    {
        if (MyPetEntity() is not { } petView || reply.AttackerId != petView.Id) return;
        fightTargetId = reply.TargetId;
        fightSeenAt = context.Now;
    }

    private void OnPetSkillMagic(MagicReply reply)
    {
        if (reply.Sub is not (MagicSub.Fail or MagicSub.Cancel)) return;
        bool pending = petSkillCasts.TryGetValue(reply.SkillId, out var cast) && cast.CasterId == reply.CasterId;
        bool current = MyPetEntity() is { } actor && actor.Id == reply.CasterId && PetSkillReadyAt.ContainsKey(reply.SkillId);
        if (!pending && !current) return;
        petSkillCasts.Remove(reply.SkillId);
        PetSkillReadyAt.Remove(reply.SkillId);
    }

    internal void BuildPetBar()
    {
        PetBar = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        PetBar.AddThemeStyleboxOverride("panel", UiTheme.Chip());
        PetBarLayer.AddChild(PetBar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", PetBarGap);
        PetBar.AddChild(row);

        var grip = new HotGrip
        {
            TooltipText = "Drag to move the familiar bar",
            CustomMinimumSize = new Vector2(HotbarFeature.HotGripThickness, PetBarSlotSize),
        };
        row.AddChild(grip);

        petAttackCell = new PetSkillCell(PetBarSlotSize) { OnUse = UsePetSkill };
        petAttackCell.Set(PetSkills.DesignatedAttack, locked: false, dim: false, "Send your familiar at your target");
        row.AddChild(petAttackCell);
        row.AddChild(UiTheme.Rule(vertical: true));

        for (int i = 0; i < PetSkills.SlotsPerPage; i++)
        {
            var cell = new PetSkillCell(PetBarSlotSize)
            {
                OnUse = UsePetSkill, Draggable = true, OnDrop = DropOnBar, OnClear = ClearFromBar, Slot = i, BagItemAt = BagItemAt,
                OnRightClick = ToggleAutoCast,
            };
            petCells.Add(cell);
            row.AddChild(cell);
        }

        petPageBtn = UiTheme.SmallButton("1", "Next page of familiar skills");
        petPageBtn.CustomMinimumSize = new Vector2(22, PetBarSlotSize);
        petPageBtn.Pressed += () =>
        {
            PetBarPage = (PetBarPage + 1) % PetSkills.Pages;
            RefreshPetBar();
        };
        row.AddChild(petPageBtn);

        HudLayout.Attach(PetBar, PetBarLayoutId, grip, PetBarDefaultPosition);
    }

    private Vector2 PetBarDefaultPosition()
    {
        var size = PetBar.GetCombinedMinimumSize();
        if (hotbar.Box != null && GodotObject.IsInstanceValid(hotbar.Box))
            return new Vector2(hotbar.Box.Position.X, Mathf.Max(0f, hotbar.Box.Position.Y - size.Y - PetBarGap * 2));
        var viewport = context.Root.GetViewport().GetVisibleRect().Size;
        return new Vector2((viewport.X - size.X) * 0.5f, viewport.Y - size.Y - HotbarFeature.HotSlotSize * 2);
    }

    internal void OnPetBarSummoned(PetSheet sheet)
    {
        var skills = new List<(int, int)>();
        foreach (var s in SkillData.All)
            if (s.Id is >= PetSkills.FirstSkill and <= PetSkills.LastSkill) skills.Add((s.Id, s.Tree));
        var owned = PetSkills.Owned(skills, sheet.Class);
        FamiliarSkills = owned;
        layoutPath = CharacterFiles.PathFor(LayoutFileKind, self.MyCharId);
        Layout = PetBarLayout.Parse(CharacterFiles.Read(layoutPath)) ?? PetBarLayout.InIdOrder(owned);
        var ownedSet = new HashSet<int>(owned);
        Layout.KeepOnly(id => ownedSet.Contains(id) || IsBarItem(id));
        PetBarPage = 0;
        PetBar.Visible = true;
        RefreshPetBar();
        FamiliarSkillsChanged?.Invoke();
    }

    private void DropOnBar(int slot, int id, int fromSlot)
    {
        if (!FamiliarSkills.Contains(id) && !IsBarItem(id))
        {
            if (id > 0) context.Log.Notice(GameText.System(TextNotPetSkill, "It is not a Familiar skill."));
            return;
        }
        if (!Layout.Place(slot, id, fromSlot)) return;
        SaveLayout();
        RefreshPetBar();
    }

    private int BagItemAt(int abs) =>
        abs >= Inventory.GridStart && abs < self.Inventory.Length && !self.Inventory[abs].IsEmpty ? self.Inventory[abs].ItemId : 0;

    private static bool IsBarItem(int id) =>
        !SkillData.IsSkill(id) && ItemData.Get(id) is { } item && PetSkills.FitsBarAsItem(item.Slot, item.Effect1);

    private void ClearFromBar(int slot)
    {
        if (!Layout.Clear(slot)) return;
        SaveLayout();
        RefreshPetBar();
    }

    private void ToggleAutoCast(int slot)
    {
        if (SkillData.Get(Layout[slot]) is not { AutoCast: true } || !Layout.ToggleAuto(slot)) return;
        SaveLayout();
        RefreshPetBar();
    }

    private void SaveLayout() => CharacterFiles.Write(layoutPath, Layout.Write());

    private void OnPetBarExp(long _) => RefreshPetBar();

    private void HidePetBar()
    {
        petSkillGeneration++;
        if (GodotObject.IsInstanceValid(PetBar)) PetBar.Visible = false;
        PetSkillReadyAt.Clear();
        petSkillCasts.Clear();
        fightTargetId = NoTarget;
    }

    internal void RefreshPetBar()
    {
        if (pet.Pet is not { } sheet || !GodotObject.IsInstanceValid(PetBar)) return;
        petPageBtn.Text = (PetBarPage + 1).ToString();
        int first = PetBarPage * PetSkills.SlotsPerPage;
        for (int i = 0; i < petCells.Count; i++)
        {
            int index = first + i;
            petCells[i].Slot = index;
            if (IsBarItem(Layout[index]))
            {
                petCells[i].SetItem(Layout[index], ItemData.DisplayName(Layout[index]));
                continue;
            }
            if (SkillData.Get(Layout[index]) is not { } s)
            {
                petCells[i].Set(0, locked: false, dim: false, "");
                continue;
            }
            bool locked = !PetSkills.Unlocked(s.Level, sheet.Level);
            string tip = SkillText.Tooltip(s) + "\n" + PetSkills.AutoCastLine(s.AutoCast) + (locked ? $"\nFamiliar level {s.Level}" : "");
            petCells[i].Set(s.Id, locked, dim: locked || sheet.Mp < s.Msp, tip);
        }
    }

    internal void PetBarTick(double now)
    {
        if (!GodotObject.IsInstanceValid(PetBar) || !PetBar.Visible) return;
        petAttackCell.SetCooldown(PetCooldown(PetSkills.DesignatedAttack, now));
        int frame = PetSkills.AutoFrameAt(now);
        foreach (var cell in petCells)
        {
            cell.SetCooldown(cell.SkillId == 0 ? 0f : PetCooldown(cell.SkillId, now));
            cell.SetAutoFrame(Layout.IsAuto(cell.Slot) ? frame : -1);
        }
        AutoCastTick(now);
    }

    private void AutoCastTick(double now)
    {
        if (now < autoCastAt || fightTargetId == NoTarget) return;
        if (now - fightSeenAt > FightMemorySeconds
            || !context.Entities.Views.TryGetValue(fightTargetId, out var target) || target.Dead || !target.Attackable)
        {
            fightTargetId = NoTarget;
            return;
        }
        if (!context.Alive || context.Self.Dead || !Game.Connection.Connected) return;
        if (pet.Pet is not { } sheet || MyPetEntity() is not { } petView) return;

        int skillId = PetSkills.NextAutoCast(Layout, id => AutoCastReady(id, sheet, now));
        if (skillId == 0 || SkillData.Get(skillId) is not { } s) return;
        int aim = s.Moral == PetSkills.OwnerMoral ? context.Self.CharId : fightTargetId;
        if (CastPetSkill(s, sheet, petView, aim)) autoCastAt = now + AutoCastSpacingSeconds;
    }

    private bool AutoCastReady(int skillId, PetSheet sheet, double now) =>
        SkillData.Get(skillId) is { AutoCast: true } s
        && PetSkills.BelongsTo(skillId, s.Tree, sheet.Class)
        && PetSkills.Unlocked(s.Level, sheet.Level)
        && sheet.Mp >= s.Msp
        && !(PetSkillReadyAt.TryGetValue(skillId, out var readyAt) && readyAt > now);

    private float PetCooldown(int skillId, double now)
    {
        if (!PetSkillReadyAt.TryGetValue(skillId, out var readyAt) || readyAt <= now) return 0f;
        float total = SkillData.Get(skillId) is { } s ? Mathf.Max(s.RecastSeconds, s.CastSeconds) : 0f;
        return total <= 0f ? 0f : Mathf.Clamp((float)(readyAt - now) / total, 0f, 1f);
    }

    internal EntityView? MyPetEntity()
    {
        if (pet.Pet is not { } sheet) return null;
        foreach (var e in context.Entities.Views.Values)
            if (e.IsNpc && e.NpcType == NpcTypes.Pet && !e.Dead && e.Name == sheet.Name) return e;
        return null;
    }

    private void UsePetSkill(int skillId)
    {
        if (!context.Alive || !context.Root.IsInsideTree() || context.Self.Dead || !Game.Connection.Connected) return;
        if (IsBarItem(skillId))
        {
            hotbar.UseHotItem(skillId);
            return;
        }
        if (pet.Pet is not { } sheet || MyPetEntity() is not { } petView)
        {
            context.Log.Notice(GameText.System(TextPetNotSummoned, "Familiar has not been summoned."));
            return;
        }
        if (SkillData.Get(skillId) is not { } s || !PetSkills.BelongsTo(skillId, s.Tree, sheet.Class))
        {
            context.Log.Notice(GameText.System(TextNotPetSkill, "It is not a Familiar skill."));
            return;
        }
        if (!PetSkills.Unlocked(s.Level, sheet.Level))
        {
            context.Log.Notice($"Your familiar needs level {s.Level} for {s.Name}.");
            return;
        }
        double now = context.Now;
        if (PetSkillReadyAt.TryGetValue(skillId, out var readyAt) && readyAt > now) return;
        if (sheet.Mp < s.Msp)
        {
            context.Log.Notice(PetNoManaText);
            return;
        }

        int target;
        if (s.Moral == PetSkills.OwnerMoral) target = context.Self.CharId;
        else if (context.Selection.SelectedId >= 0 && context.Entities.Views.TryGetValue(context.Selection.SelectedId, out var t) && t.IsNpc && t.Attackable && !t.Dead)
            target = context.Selection.SelectedId;
        else
        {
            context.Log.Notice(PetNoTargetText);
            return;
        }

        CastPetSkill(s, sheet, petView, target);
    }

    private bool CastPetSkill(SkillData.Skill s, PetSheet sheet, EntityView petView, int target)
    {
        int skillId = s.Id;
        int casterId = petView.Id, x = (int)petView.MapX, y = (int)petView.MapY, z = (int)petView.MapZ;
        double readyAgain = context.Now + Mathf.Max(s.RecastSeconds, s.CastSeconds);
        if (s.CastSeconds <= 0f)
        {
            if (!petRequests.Skill(PetSkills.StageEffecting, skillId, casterId, target, x, y, z)) return false;
            PetSkillReadyAt[skillId] = readyAgain;
            return true;
        }

        if (!petRequests.Skill(PetSkills.StageCasting, skillId, casterId, target, x, y, z)) return false;
        PetSkillReadyAt[skillId] = readyAgain;
        int petIndex = sheet.Index, generation = petSkillGeneration, serial = ++petSkillCastSerial;
        petSkillCasts[skillId] = new PetSkillCast(casterId, serial);
        context.Root.After(s.CastSeconds, () =>
        {
            if (!context.Alive || !petSkillCasts.TryGetValue(skillId, out var cast) || cast != new PetSkillCast(casterId, serial)) return;
            petSkillCasts.Remove(skillId);
            if (CanFinishPetSkill(petIndex, casterId, generation))
                petRequests.Skill(PetSkills.StageEffecting, skillId, casterId, target, x, y, z);
        });
        return true;
    }

    private bool CanFinishPetSkill(int petIndex, int casterId, int generation) =>
        context.Alive && context.Root.IsInsideTree() && !context.Self.Dead && generation == petSkillGeneration
        && Game.Connection.Connected && pet.Pet is { } sheet && sheet.Index == petIndex
        && MyPetEntity() is { } caster && caster.Id == casterId;
}
