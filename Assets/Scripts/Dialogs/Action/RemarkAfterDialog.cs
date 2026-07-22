using UnityEngine;

public class RemarkAfterDialog : DialogAction
{
    [SerializeField] private EnumData.RemarksType _remark;

    public override void Action(GameManager manager)
    {
        manager.Dialog.Remarks.StartRemark(_remark);
    }
}