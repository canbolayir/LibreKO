namespace LibreKO.Network.Readers;

internal sealed class ManesSurvivalReader : IPacketReader
{
    private readonly ManesSurvivalSession manes;
    private readonly PetSession pet;
    private readonly LootSession loot;
    private readonly ItemsSession items;
    private readonly SelfSession self;

    public ManesSurvivalReader(ManesSurvivalSession manes, PetSession pet, LootSession loot, ItemsSession items, SelfSession self)
    {
        this.manes = manes;
        this.pet = pet;
        this.loot = loot;
        this.items = items;
        this.self = self;
    }

    public void Register(PacketDispatcher dispatcher)
    {
        dispatcher.On(GameOpcodes.GS_MANES_SURVIVAL, HandleManesSurvival);
    }

    private void HandleManesSurvival(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch ((ManesSurvivalSubOpcode)p.ReadByte())
        {
            case ManesSurvivalSubOpcode.SignUp:
                manes.RaiseSignUp(ManesSurvivalWire.ReadSignUp(p));
                break;
            case ManesSurvivalSubOpcode.State:
                HandleState(p);
                break;
            case ManesSurvivalSubOpcode.Notice:
                HandleNotice(p);
                break;
            case ManesSurvivalSubOpcode.Progress:
                HandleProgress(p);
                break;
            case ManesSurvivalSubOpcode.Bag:
                HandleBag(p);
                break;
            case ManesSurvivalSubOpcode.Pick:
                HandlePick(p);
                break;
        }
    }

    private void HandleState(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch ((ManesStateKind)p.ReadByte())
        {
            case ManesStateKind.Start:
                manes.ApplyStart(ManesSurvivalWire.ReadStart(p));
                break;
            case ManesStateKind.Boss:
                manes.RaiseBoss(ManesSurvivalWire.ReadBoss(p));
                break;
            case ManesStateKind.Rank:
                manes.RaiseRank(ManesSurvivalWire.ReadRank(p));
                break;
            case ManesStateKind.End:
                manes.ApplyEnd(ManesSurvivalWire.ReadEnd(p, pet.Remember));
                break;
        }
    }

    private void HandleNotice(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch ((ManesNoticeKind)p.ReadByte())
        {
            case ManesNoticeKind.Markers:
                manes.RaiseMarkers(ManesSurvivalWire.ReadMarkers(p));
                break;
            case ManesNoticeKind.LastBoss:
                if (p.RemainingBytes >= 2) manes.RaiseLastBoss(p.ReadString());
                break;
            case ManesNoticeKind.Kill:
                manes.RaiseKill(ManesSurvivalWire.ReadKill(p));
                break;
            case ManesNoticeKind.Reward:
                manes.RaiseReward(ManesSurvivalWire.ReadReward(p));
                break;
            case ManesNoticeKind.Invincible:
                manes.RaiseInvincible();
                break;
        }
    }

    private void HandleProgress(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch ((ManesProgressKind)p.ReadByte())
        {
            case ManesProgressKind.Exp:
                if (p.RemainingBytes >= 3) manes.ApplyExp(ManesSurvivalWire.ReadExp(p));
                break;
            case ManesProgressKind.Level:
                if (p.RemainingBytes >= 17) manes.ApplyLevel(ManesSurvivalWire.ReadLevel(p));
                break;
            case ManesProgressKind.Score:
                if (p.RemainingBytes >= 4) manes.ApplyScore(p.ReadInt());
                break;
        }
    }

    private void HandleBag(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch ((ManesBagKind)p.ReadByte())
        {
            case ManesBagKind.Loot:
                if (p.RemainingBytes >= 12) HandleLoot(ManesSurvivalWire.ReadLoot(p));
                break;
            case ManesBagKind.Put:
                manes.ApplyBagItem(ManesSurvivalWire.ReadBagItem(p));
                break;
            case ManesBagKind.Discard:
                if (p.RemainingBytes >= 1) HandleDiscard(p.ReadByte() == 1);
                break;
        }
    }

    private void HandleLoot(ManesLoot taken)
    {
        manes.RaiseLoot(taken);
        if (taken.Position >= 0) loot.RaiseLootTaken(taken.BundleId, ManesSurvivalWire.UnknownBundleSlot, taken.ItemId);
    }

    private void HandleDiscard(bool done)
    {
        if (done && items.PendingRemoveSlot >= 0)
        {
            int slot = items.PendingRemoveSlot;
            self.SetInventorySlot(slot, default);
            items.RaiseInventorySlot(slot, default);
        }
        items.ApplyRemoveResult(done, ItemRemoveReason.SystemError);
        manes.RaiseBagReply(done);
    }

    private void HandlePick(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch ((ManesPickKind)p.ReadByte())
        {
            case ManesPickKind.Offer:
                manes.ApplyOffer(ManesSurvivalWire.ReadOffer(p, bonus: false));
                break;
            case ManesPickKind.BonusOffer:
                manes.ApplyOffer(ManesSurvivalWire.ReadOffer(p, bonus: true));
                break;
            case ManesPickKind.Choice:
                manes.RaisePickReply(ManesSurvivalWire.ReadPickReply(p, bonus: false));
                break;
            case ManesPickKind.BonusChoice:
                manes.RaisePickReply(ManesSurvivalWire.ReadPickReply(p, bonus: true));
                break;
            case ManesPickKind.Passive:
                if (p.RemainingBytes >= 2) manes.ApplyPassive(p.ReadShort());
                break;
        }
    }
}
