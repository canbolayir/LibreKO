namespace LibreKO.Network.Requests;

public interface IBifrostRequests
{
    void FullMoonEnter();
    void TimeRequest();
    void Join(TempleEventType type);
    void Disband(TempleEventType type);
    void DrakiRankList();
    void DrakiEnter(int certificateItem, int chapter);
    void DrakiTown();
}

public sealed class BifrostRequests : IBifrostRequests
{
    private readonly IPacketSink sink;

    public BifrostRequests(IPacketSink sink) => this.sink = sink;

    public void DrakiRankList()
    {
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.DrakiList);
        sink.Send(p);
    }

    public void DrakiEnter(int certificateItem, int chapter) => sink.Send(DrakiWire.Enter(certificateItem, chapter));

    public void DrakiTown()
    {
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.DrakiTown);
        sink.Send(p);
    }

    public void FullMoonEnter()
    {
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.FullMoonEntry);
        p.WriteByte(Domain.FullMoon.EntryKeyA);
        p.WriteByte(Domain.FullMoon.EntryKeyB);
        sink.Send(p);
    }

    public void TimeRequest()
    {
        var p = new Packet(GameOpcodes.GS_BIFROST);
        p.WriteByte((byte)TempleSubOpcode.BifrostRemaining);
        sink.Send(p);
    }

    public void Join(TempleEventType type)
    {
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.TempleEventJoin);
        p.WriteUShort((ushort)TempleJoin.IndunType(type));
        sink.Send(p);
    }

    public void Disband(TempleEventType type)
    {
        var p = new Packet(GameOpcodes.GS_EVENT);
        p.WriteByte((byte)TempleSubOpcode.TempleEventDisband);
        p.WriteUShort((ushort)TempleJoin.IndunType(type));
        sink.Send(p);
    }
}
