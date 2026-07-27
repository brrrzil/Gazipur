using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class QuestManager : MonoBehaviour
{
    public Dictionary<Quests, int> QuestsState { get; private set; }

    [SerializeField] private GameObject _filterPanel;
    [SerializeField] private GameObject _blueprintPanel;
    [SerializeField] private GameObject _filterPlace;
    [SerializeField] private GameObject _filterObject;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Toggle _medecineCheckBox;
    [Inject] Inventory _inventory;
    [Inject] DialogManager _dialog;
    [Inject] DataManager _data;
    [Inject] GameModeManager _mode;
    [Inject] Sounds _sounds;
    private bool _isStartFind;
    private void Start()
    {
        QuestsState = new Dictionary<Quests, int>()
        {
            [Quests.filter] = 0,
            [Quests.healMother] = 0
        };
        _filterObject.SetActive(false);
        _blueprintPanel.SetActive(false);
        _filterPlace.SetActive(false);
        _medecineCheckBox.gameObject.SetActive(false);
        _mode.onChangeMode += m =>
        {
            if (m == GameMode.trade)
                _isStartFind = true;
        };
        _inventory.onTakeItem += i =>
        {
            if (_isStartFind && QuestsState[Quests.filter] == 0
            && _data.gameMode == GameMode.outdors && !(i.ItemPrefab is FilterPart))
            {
                QuestsState[Quests.filter] = 1;
                _filterPanel.SetActive(true);
                _blueprintPanel.SetActive(true);
                _dialog.Remarks.StartRemark(RemarksType.foundBlueprint);
                _filterPlace.SetActive(true);
                _mode.ChangeMode(GameMode.otherPanels);
            }
            if (QuestsState[Quests.filter] == 1 && _inventory.CheckFilterBlueprint(i))
            {
                _dialog.Remarks.StartRemark(RemarksType.foundPart);
            }
        };
    }

    public void CloseFilterPanel()
    {
        _dialog.Remarks.StartRemark(RemarksType.closeBlueprint);
        _filterPanel.SetActive(false);
        _mode.ChangeMode(GameMode.outdors);
    }
    public void HealMother(bool isHeal)
    {
        if (!isHeal)
        {
            _medecineCheckBox.gameObject.SetActive(true);
            QuestsState[Quests.healMother] = 1;
        }
        else
        {
            QuestsState[Quests.healMother] = 2;
            _medecineCheckBox.isOn = true;
        }
    }
    public void CompleteFilter()
    {
        // Use the dedicated `win` GameMode (added in round 10) so that
        // PlayerMovement.SetMode treats this as a UI mode and the player
        // can't move while the win panel is up. WinDiePanel.ContinueButton
        // returns the game to outdors when the player clicks Continue.
        _mode.ChangeMode(GameMode.win);
        winPanel.SetActive(true);
        _blueprintPanel.SetActive(false);
        _filterPlace.SetActive(true);
        QuestsState[Quests.filter] = 2;

        // BUGFIX: переключаем фоновую музыку на трек победы
        _sounds.SwitchToWinBackground();
    }
}