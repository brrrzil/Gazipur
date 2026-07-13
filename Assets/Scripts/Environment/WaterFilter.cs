using UnityEngine;
using Zenject;

public class WaterFilter : InteractObject
{
    [SerializeField] private float _makeTime;
    [SerializeField] private EnumData.PlayerSound _buildSound;
    [SerializeField] private FilterBlueprint _blueprint;
    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;
    [Inject] private Sounds _sounds;
    [Inject] HoldProgressBar _holdBar;

    public override void Intearct(bool isDown)
    {
        Debug.Log("filterComplete " + _blueprint.CheckComplete());
        if (!_blueprint.CheckComplete())
        {
                _dialog.Remarks.StartRemark(EnumData.RemarksType.fewParts);
        }
        else if(_quest.QuestsState[EnumData.Quests.healMother] == 2)
        {
            if (isDown)
            {
                _holdBar.StartHold(_makeTime);
                _holdBar.OnHoldComplete += Finish;
                PlayBuildSound();
            }
            else
            {
                _sounds.PlayerStop();
                _holdBar.CancelHold();
                _holdBar.OnHoldComplete -= Finish;
            }
        }
        else
        {
            _dialog.Remarks.StartRemark(EnumData.RemarksType.firstMother);
        }
    }
    private void Finish()
    {
        _sounds.PlayerStop();
        _quest.CompleteFilter();
    }

    // Play the build sound in a loop while the player holds the 'use' button.
    // Mirrors the pattern used by HoleInFance (fence cut) and GarbageObject
    // (loot pick) so the clip is routed through the same game audio mixer
    // and shares the existing _playerSource on the Sounds service.
    // isLoop = true: the clip must keep playing until the hold completes or
    // is cancelled, since _makeTime is several seconds long.
    private void PlayBuildSound()
    {
        _sounds.PlayerPlay(_buildSound, true);
    }
}
