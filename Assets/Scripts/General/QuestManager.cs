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
    [Tooltip("Ending cutscene played when the filter is completed. Optional - if empty the win panel is shown directly. Wire the cutscene's _onFinished event to ShowWinPanel() so the panel appears after the sequence ends.")]
    [SerializeField] private CutsceneController _cutscene;
    [Inject] Inventory _inventory;
    [Inject] DialogManager _dialog;
    [Inject] DataManager _data;
    [Inject] GameModeManager _mode;
    [Inject] Sounds _sounds;
    // MapUI is NOT injected: it doesn't exist in the scene at
    // SceneContext.Resolve() time. The map is an in-world item
    // the player picks up first; MapUI gets created and assigns
    // MapUI.Instance in its own Awake later. By the time the
    // FoundBlueprint event fires, the player already has the map
    // and MapUI.Instance is set, so the direct lookup is safe.
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
                // First loot after talking to the trader → the
                // skimmer NPC activates in the world and its map
                // icon unlocks. MapUI persists the unlock so it
                // survives scene reloads and restarts. Use
                // MapUI.Instance directly because the map hasn't
                // been picked up by the player at scene start, so
                // [Inject] would resolve to null.
                MapUI.Instance?.UnlockClampIcon("Icon_Skimmer");
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
        _blueprintPanel.SetActive(false);
        _filterPlace.SetActive(true);
        QuestsState[Quests.filter] = 2;

        // BUGFIX: ����������� ������� ������ �� ���� ������
        _sounds.SwitchToWinBackground();

        // If a cutscene is wired up, it owns the screen: it plays the
        // Timeline and then shows the win panel via its _onFinished
        // event (wire CutsceneController._onFinished -> ShowWinPanel).
        // Without a cutscene we fall back to the old behaviour so the
        // game can never soft-lock at the ending.
        if (_cutscene != null)
        {
            _cutscene.Play();
        }
        else
        {
            ShowWinPanel();
        }
    }

    // Show the win panel. Public so it can be wired to
    // CutsceneController._onFinished in the Inspector.
    public void ShowWinPanel()
    {
        // Use the dedicated `win` GameMode (added in round 10) so that
        // PlayerMovement.SetMode treats this as a UI mode and the player
        // can't move while the win panel is up. WinDiePanel.ContinueButton
        // returns the game to outdors when the player clicks Continue.
        _mode.ChangeMode(GameMode.win);
        winPanel.SetActive(true);
    }
}