using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class QuestManager : MonoBehaviour
{
    // (r4 / save-quests) Singleton accessor so GamePersistence can read
    // QuestsState on save and apply saved states on load without having
    // to FindAnyObjectByType at the right moment.
    public static QuestManager Instance { get; private set; }

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
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
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

    // (r4 / save-quests) Replay quest progression from a saved blob.
    // Called by GamePersistence.LoadIntoGame after the QuestManager has
    // finished its Awake/Start, so the dictionary already exists and
    // the SerializeField UI references are wired.
    public void ApplyQuestStatesFromSave(System.Collections.Generic.IEnumerable<(EnumData.Quests quest, int value)> saved)
    {
        if (saved == null) return;
        foreach (var (quest, value) in saved)
        {
            if (QuestsState.ContainsKey(quest))
                QuestsState[quest] = value;
            else
                QuestsState.Add(quest, value);

            // Mirror the side-effects the in-game quest progression does
            // - we only push the visible UI bits, the rest is driven by
            //   the player's actions when they continue playing.
            if (quest == EnumData.Quests.healMother && value >= 1)
                _medecineCheckBox.gameObject.SetActive(true);
            if (quest == EnumData.Quests.healMother && value >= 2)
                _medecineCheckBox.isOn = true;
            if (quest == EnumData.Quests.filter && value >= 1)
            {
                _filterPanel.SetActive(true);
                _blueprintPanel.SetActive(true);
                _filterPlace.SetActive(true);
            }
            if (quest == EnumData.Quests.filter && value >= 2)
            {
                _filterObject.SetActive(true);
            }
        }
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

        // BUGFIX: ����������� ������� ������ �� ���� ������
        _sounds.SwitchToWinBackground();
    }

    // (r5 / new-game) Resets quest progression to scene defaults and
    // hides every quest UI panel. Called by GameSession.ResetForNewGame
    // when the player hits 'New Game' from the main menu.
    public void ResetToSceneDefaults()
    {
        if (QuestsState != null)
        {
            QuestsState[Quests.filter] = 0;
            QuestsState[Quests.healMother] = 0;
        }
        if (_filterObject != null) _filterObject.SetActive(false);
        if (_blueprintPanel != null) _blueprintPanel.SetActive(false);
        if (_filterPlace != null) _filterPlace.SetActive(false);
        if (_medecineCheckBox != null)
        {
            _medecineCheckBox.isOn = false;
            _medecineCheckBox.gameObject.SetActive(false);
        }
        _isStartFind = false;
        Debug.Log("[QuestManager] ResetToSceneDefaults done");
    }
}