using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibreKO.Domain;

namespace LibreKO.Network;

public sealed record NationTransferCandidate(int Slot, string Name, int Race, int Nation, int Class, int Face, int Hair);

public readonly record struct NationTransferPick(int Slot, string Name, int Race, int Face, int Hair);

public partial class Net
{
    public const byte NationTransferWarStatus = 1;
    public const byte NationTransferOpenBox = 2;
    public const byte NationTransferSubmit = 3;
    public const byte NationTransferAccepted = 1;
    public const byte NationTransferMovedAgain = 2;
    public const byte NationTransferErrorBox = 16;
    public const byte NationTransferWarRunning = 8;

    public event Action<IReadOnlyList<NationTransferCandidate>>? NationTransferOpenEvent;
    public event Action<int>? NationTransferRefusedEvent;
    public event Action? NationTransferDoneEvent;
    public event Action<int, int>? NationTransferWarEvent;
    public event Action? NationTransferResetEvent;

    private int _nationTransferTarget;
    private bool _nationTransferPending;
    private IReadOnlyList<NationTransferCandidate> _nationTransferCandidates = Array.Empty<NationTransferCandidate>();

    private void HandleNationTransfer(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        byte sub = p.ReadByte();
        byte result = p.ReadByte();
        switch (sub)
        {
            case NationTransferOpenBox when result == NationTransferAccepted:
                if (_nationTransferPending || _nationTransferTarget != 0) return;
                var candidates = ReadNationTransferCandidates(p);
                if (candidates == null) return;
                _nationTransferCandidates = candidates;
                _nationTransferTarget = candidates.Count == 0 ? 0 : candidates[0].Nation;
                NationTransferOpenEvent?.Invoke(candidates);
                break;
            case NationTransferSubmit when result is NationTransferAccepted or NationTransferMovedAgain:
                if (!_nationTransferPending) return;
                _nationTransferPending = false;
                Nation = _nationTransferTarget;
                _nationTransferTarget = 0;
                _nationTransferCandidates = Array.Empty<NationTransferCandidate>();
                NationTransferDoneEvent?.Invoke();
                break;
            case NationTransferWarStatus or NationTransferErrorBox when result == NationTransferWarRunning && p.RemainingBytes >= 2:
                _nationTransferPending = false;
                NationTransferWarEvent?.Invoke(p.ReadByte(), p.ReadByte());
                break;
            case NationTransferWarStatus or NationTransferOpenBox or NationTransferSubmit or NationTransferErrorBox:
                if (result is not (0 or 2 or 3 or 4 or 5 or 6 or 7 or 9 or 10)) return;
                if (sub == NationTransferSubmit && !_nationTransferPending) return;
                _nationTransferPending = false;
                NationTransferRefusedEvent?.Invoke(result);
                break;
        }
    }

    private static List<NationTransferCandidate>? ReadNationTransferCandidates(Packet p)
    {
        var list = new List<NationTransferCandidate>();
        if (p.RemainingBytes < 1) return null;
        var slots = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            int count = p.ReadByte();
            for (int i = 0; i < count; i++)
            {
                int slot = p.ReadShort();
                string name = p.ReadString();
                int race = p.ReadByte(), nation = p.ReadByte(), cls = p.ReadShort(), face = p.ReadByte();
                int hair = p.ReadInt();
                if (slot < 0 || !slots.Add(slot) || name.Length is < 1 or > 20 || !names.Add(name)
                    || nation is not 1 and not 2 || cls / 100 != nation
                    || !GenderChange.AllowedRaces(cls).Contains(race)
                    || list.Count > 0 && list[0].Nation != nation) return null;
                list.Add(new NationTransferCandidate(slot, name, race, nation, cls, face, hair));
            }
        }
        catch (InvalidDataException) { return null; }
        return p.RemainingBytes == 0 ? list : null;
    }

    public bool SendNationTransfer(IReadOnlyList<NationTransferPick> picks)
    {
        if (_nationTransferPending || _nationTransferTarget is not 1 and not 2
            || picks.Count == 0 || picks.Count != _nationTransferCandidates.Count || picks.Count > 255
            || picks.Select(pick => pick.Slot).Distinct().Count() != picks.Count) return false;
        foreach (var pick in picks)
        {
            var candidate = _nationTransferCandidates.FirstOrDefault(candidate => candidate.Slot == pick.Slot);
            if (candidate == null || candidate.Name != pick.Name || pick.Face is < 0 or > 255
                || !GenderChange.AllowedRaces(candidate.Class).Contains(pick.Race)) return false;
        }
        var p = new Packet(GameOpcodes.GS_NATION_TRANSFER);
        p.WriteByte(NationTransferSubmit);
        p.WriteByte(NationTransferAccepted);
        p.WriteByte((byte)picks.Count);
        foreach (var pick in picks)
        {
            p.WriteShort((short)pick.Slot);
            p.WriteString(pick.Name);
            p.WriteByte((byte)pick.Race);
            p.WriteByte((byte)pick.Face);
            p.WriteInt(pick.Hair);
        }
        _nationTransferPending = true;
        _conn.Send(p);
        return true;
    }

    public void SendNationTransferCancel()
    {
        if (_nationTransferPending) return;
        _nationTransferTarget = 0;
        _nationTransferCandidates = Array.Empty<NationTransferCandidate>();
        var p = new Packet(GameOpcodes.GS_NATION_TRANSFER);
        p.WriteByte(NationTransferSubmit);
        p.WriteByte(0);
        _conn.Send(p);
    }

    private void ResetNationTransfer()
    {
        bool hadOperation = _nationTransferPending || _nationTransferTarget != 0;
        _nationTransferPending = false;
        _nationTransferTarget = 0;
        _nationTransferCandidates = Array.Empty<NationTransferCandidate>();
        if (hadOperation) NationTransferResetEvent?.Invoke();
    }
}
