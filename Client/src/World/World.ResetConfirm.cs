using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private byte _resetKind;
    private bool _resetAwaitingCost;
    private Notice? _resetConfirm;

    private void ResetConfirmInit()
    {
        Net.I.ResetCostEvent += OnResetCost;
        Net.I.StatResetEvent += OnStatResetFinished;
        Net.I.SkillResetEvent += OnSkillResetFinished;
    }

    private void ResetConfirmDispose()
    {
        Net.I.ResetCostEvent -= OnResetCost;
        Net.I.StatResetEvent -= OnStatResetFinished;
        Net.I.SkillResetEvent -= OnSkillResetFinished;
        DismissResetConfirm();
    }

    private void RequestReset(byte kind)
    {
        if (_resetKind != 0) return;
        _resetKind = kind;
        _resetAwaitingCost = true;
        _classChangePanel?.SetMeta("redistribution_pending", true);
        Net.I.SendResetCostQuery(kind);
    }

    private void OnResetCost(int cost)
    {
        if (_resetKind == 0 || !_resetAwaitingCost) return;
        _resetAwaitingCost = false;
        DismissResetConfirm();

        bool stat = _resetKind == Net.ResetKindStat;
        string what = stat ? "stat points" : "mastery points";
        string body = $"Every one of your {what} goes back into the pool, and it costs "
                      + $"{cost:n0} gold.";
        if (stat)
            body += "\n\nUnequip every item first.";

        _resetConfirm = Notice.Confirm(
            this,
            body,
            "Redistribute",
            "Cancel",
            ConfirmReset,
            CancelReset,
            stat ? "Redistribute stats" : "Redistribute mastery");
    }

    private void ConfirmReset()
    {
        if (_resetKind == 0 || _resetAwaitingCost) return;
        _resetConfirm = null;
        if (_resetKind == Net.ResetKindStat) Net.I.SendStatReset();
        else Net.I.SendSkillReset();
    }

    private void CancelReset()
    {
        _resetConfirm = null;
        _resetKind = 0;
        _resetAwaitingCost = false;
        _classChangePanel?.SetMeta("redistribution_pending", false);
    }

    private void DismissResetConfirm()
    {
        if (_resetConfirm != null && GodotObject.IsInstanceValid(_resetConfirm))
            _resetConfirm.Close();
        _resetConfirm = null;
    }

    private void OnStatResetFinished(
        bool ok, int money, int[] stats, int maxHp, int maxMp, int ap, int statPoints)
    {
        if (!ok)
        {
            CombatNotice(money > 0
                ? $"The redistribution needs {money:n0} gold, and empty equipment slots."
                : "There is nothing to redistribute.");
            CancelReset();
            return;
        }

        CancelReset();
    }

    private void OnSkillResetFinished(bool ok, int money, int pool)
    {
        if (!ok)
        {
            CombatNotice(money > 0
                ? $"The redistribution needs {money:n0} gold."
                : "There is nothing to redistribute.");
            CancelReset();
            return;
        }

        CancelReset();
    }
}
