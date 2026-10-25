namespace LibreKO.Network.Requests;

public interface IManesSurvivalRequests
{
    void SignUp(bool apply);
    void Rank();
    void Pick(int code);
    void BuyPotion(int code);
    void SkipPick();
    void BonusPick(int code);
    void Loot(int bundleId, int itemId, int index);
    void Discard(int bagSlot, int itemId);
}

public sealed class ManesSurvivalRequests : IManesSurvivalRequests
{
    private readonly IPacketSink sink;
    private readonly ItemsSession items;

    public ManesSurvivalRequests(IPacketSink sink, ItemsSession items)
    {
        this.sink = sink;
        this.items = items;
    }

    public void SignUp(bool apply)
    {
        var p = Survival(ManesSurvivalSubOpcode.SignUp, (byte)ManesSignUpKind.Entry);
        p.WriteUShort((ushort)(apply ? ManesSignUpAction.Apply : ManesSignUpAction.Cancel));
        sink.Send(p);
    }

    public void Rank() => sink.Send(Survival(ManesSurvivalSubOpcode.State, (byte)ManesStateKind.Rank));

    public void Pick(int code) => SendPick(ManesPickKind.Choice, ManesPickAction.Skill, code);

    public void BuyPotion(int code) => SendPick(ManesPickKind.Choice, ManesPickAction.Potion, code);

    public void SkipPick()
    {
        var p = Survival(ManesSurvivalSubOpcode.Pick, (byte)ManesPickKind.Choice);
        p.WriteByte((byte)ManesPickAction.Skip);
        sink.Send(p);
    }

    public void BonusPick(int code) => SendPick(ManesPickKind.BonusChoice, ManesPickAction.Skill, code);

    public void Loot(int bundleId, int itemId, int index)
    {
        var p = Survival(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Loot);
        p.WriteInt(bundleId);
        p.WriteInt(itemId);
        p.WriteUShort((ushort)index);
        sink.Send(p);
    }

    public void Discard(int bagSlot, int itemId)
    {
        if (!sink.Connected) { items.RaiseItemRemoveResult(false, ItemRemoveReason.SystemError); return; }
        items.PendingRemoveSlot = InventoryConstants.InventoryStart + bagSlot;
        var p = Survival(ManesSurvivalSubOpcode.Bag, (byte)ManesBagKind.Discard);
        p.WriteByte((byte)bagSlot);
        p.WriteInt(itemId);
        sink.Send(p);
    }

    private void SendPick(ManesPickKind kind, ManesPickAction action, int code)
    {
        var p = Survival(ManesSurvivalSubOpcode.Pick, (byte)kind);
        p.WriteByte((byte)action);
        p.WriteShort((short)code);
        sink.Send(p);
    }

    private static Packet Survival(ManesSurvivalSubOpcode sub, byte kind)
    {
        var p = new Packet(GameOpcodes.GS_MANES_SURVIVAL);
        p.WriteByte((byte)sub);
        p.WriteByte(kind);
        return p;
    }
}
