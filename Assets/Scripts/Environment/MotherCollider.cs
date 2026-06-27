using UnityEngine;
using Zenject;
using static EnumData;

[RequireComponent(typeof(BoxCollider))]

public class MotherCollider : MonoBehaviour
{
    [Inject] Inventory _inventory;
    [Inject] DialogManager _dialog;
    [Inject] QuestManager _quest;
    private bool _isWork;
    private void Start()
    {
        _inventory.onTakeItem += itm => _isWork = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isWork) return;

        if (other.GetComponent<PlayerMovement>())
        {
            var medCell = _inventory.CheckMedeicine();
            if (medCell != null)
            {
                _dialog.StartDialog(DialogType.motherMedecine);
                medCell.RemoveItem();
                _quest.HealMother(true);
            }
            else if(_quest.QuestsState[Quests.healMother]!=2)
            {
                if (!_inventory.HaveTools.Contains(ToolsType.cutter))
                {
                    _dialog.Remarks.StartRemark(RemarksType.tooEarly);
                }
                else
                {
                    if (!_dialog.StartDialog(DialogType.motherDisease))
                    {
                        _dialog.Remarks.StartRemark(RemarksType.relaxMom);
                    }
                }
            }
           
        }
    }

}
