using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// (SaveSystem) The pause menu inside GameScene. Currently exposes a
/// single Save button that snapshots game state via GamePersistence
/// and writes to PlayerPrefs. Add other pause actions (settings,
/// main menu, restart) by attaching more Button references here.
/// </summary>
public class MenuPausePanel : MonoBehaviour
{
    [Tooltip("The Save button. On click it calls GamePersistence.SaveNow() which writes the current game state to PlayerPrefs.")]
    [SerializeField] private Button _saveButton;
    [Tooltip("Optional: button that exits to the main menu. Wipes the in-progress pause but keeps the save.")]
    [SerializeField] private Button _backToMenuButton;

    private void Start()
    {
        if (_saveButton != null)
            _saveButton.onClick.AddListener(OnSaveClicked);

        if (_backToMenuButton != null)
            _backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
    }

    private void OnSaveClicked()
    {
        // Snapshot the live state and write it. GamePersistence.SaveNow
        // returns void so the user gets no in-game feedback beyond the
        // button click - hook this up to a "Saved" toast if you want
        // visual confirmation.
        GamePersistence.SaveNow();
    }

    private void OnBackToMenuClicked()
    {
        // Same path the pause panel normally uses: just hide the
        // menu. The game stays paused until something unpauses it.
        gameObject.SetActive(false);
    }
}
