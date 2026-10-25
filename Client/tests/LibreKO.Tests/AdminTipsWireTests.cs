using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Network.Readers;
using LibreKO.Network.Requests;
using LibreKO.Session;
using Xunit;

namespace LibreKO.Tests;

public class AdminTipsWireTests
{
    private sealed class Recorder : IPacketSink
    {
        public readonly List<Packet> Sent = new();
        public bool Connected => true;
        public void Send(Packet packet) => Sent.Add(packet);
    }

    [Fact]
    public void TheTipListArrivesWithItsSettings()
    {
        var session = new GameSession();
        var dispatcher = new PacketDispatcher();
        PacketReaders.Register(dispatcher, session, new GameRequests(new Recorder(), session, new LoginState()), () => 0);
        AdminTips? shown = null;
        session.Admin.AdminTipsEvent += tips => shown = tips;

        var p = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        p.WriteByte((byte)AdminReplySubOpcode.Tips);
        p.WriteByte(1);
        p.WriteUShort(5);
        p.WriteUShort(2);
        p.WriteInt(1);
        p.WriteByte((byte)GameLanguage.English);
        p.WriteByte(1);
        p.WriteString("Press K to open your skills.");
        p.WriteInt(2);
        p.WriteByte((byte)GameLanguage.Spanish);
        p.WriteByte(0);
        p.WriteString("Pulsa K.");
        p.ResetOffset();
        dispatcher.Dispatch(p);

        Assert.NotNull(shown);
        Assert.True(shown.Enabled);
        Assert.Equal(5, shown.IntervalMinutes);
        Assert.Equal(
            new[] { new AdminTip(1, GameLanguage.English, true, "Press K to open your skills."), new AdminTip(2, GameLanguage.Spanish, false, "Pulsa K.") },
            shown.Tips);
    }

    [Fact]
    public void SavingATipSendsItsLanguageStateAndText()
    {
        var sink = new Recorder();

        new AdminRequests(sink).TipSave(new AdminTip(0, GameLanguage.Chinese, true, "按 K 打开技能。"));

        var p = Assert.Single(sink.Sent);
        p.ResetOffset();
        Assert.Equal((byte)AdminRequestSubOpcode.TipSave, p.ReadByte());
        Assert.Equal(0, p.ReadInt());
        Assert.Equal((byte)GameLanguage.Chinese, p.ReadByte());
        Assert.Equal(1, p.ReadByte());
        Assert.Equal("按 K 打开技能。", p.ReadString());
    }

    [Fact]
    public void TipsAreGreenLinesOfTheGeneralChat()
    {
        Assert.Equal(ChatCategory.General, ChatCategories.Of(ChatType.GameplayTip, false));
        Assert.NotEqual(new ChatColors().ForLine(ChatType.General), new ChatColors().ForLine(ChatType.GameplayTip));
    }
}
