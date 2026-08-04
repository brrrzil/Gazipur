using UnityEngine;
using Zenject;
using static EnumData;

public class DangerGarbageObject : GarbageObject
{
    [SerializeField] private ToolsType _needadTool;
    [SerializeField] private int _damageChance = 100;
    [SerializeField] private int _damage = 10;
    [Inject] private PlayerState _player;
    [Inject] private DialogManager _dialog;

    public override void Intearct(bool isDown)
    {
        // BUGFIX (round 12): if the player doesn't have the required tool,
        // don't start the progress bar at all — just play the remark. The
        // previous behaviour was to start the bar, animate it, and only
        // then refuse to loot (which was a waste of the player's time and
        // felt like a glitch).
        if (isDown && !_inventory.HaveTools.Contains(_needadTool))
        {
            _dialog.Remarks.StartRemark(
                _needadTool == ToolsType.wrench ? RemarksType.noWrench
                : _needadTool == ToolsType.hacksaw ? RemarksType.noHacksaw
                : RemarksType.noWrench);
            return;
        }
        base.Intearct(isDown);
    }

    protected override void PicItem()
    {
        // Defensive check — should never hit because Intearct filters, but
        // keeps the original safety net in case the tool inventory changes
        // between Intearct (true) and PicItem (e.g. cheat console, etc.).
        if (!_inventory.HaveTools.Contains(_needadTool))
        {
            _dialog.Remarks.StartRemark(
                _needadTool == ToolsType.wrench ? RemarksType.noWrench
                : _needadTool == ToolsType.hacksaw ? RemarksType.noHacksaw
                : RemarksType.noWrench);
            return;
        }
        int rnd = Random.Range(0, 100);
        if (!_inventory.HaveTools.Contains(ToolsType.glowes) &&  _damageChance> rnd)
        {
            _dialog.Remarks.StartRemark(RemarksType.noGlowes);
            _player.TakeDamage(_damage);
        }
        base.PicItem();
    }

}
