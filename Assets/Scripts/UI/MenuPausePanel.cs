using UnityEngine;
using UnityEngine.SceneManagement;
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

    [Tooltip("Name of the main-menu scene to load when the player clicks Back To Menu. Set in Inspector; defaults to 'MainMenu'.")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

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
        // (r5 / ddol-gamescene) Returning to the main menu has three
        // steps:
        // 1) Hide this pause panel so it isn't visible behind the
        //    menu transition.
        // 2) Deactivate the GameScene DDOL host via GameSession.
        //    DeactivateHost saves the current state (so a later
        //    Continue picks up where the player left off), shows the
        //    cursor (MainMenu buttons need it), and sets
        //    _hostActive=false so the player can't see or hear
        //    gameplay while on the menu.
        // 3) Load the MainMenu scene. Because GameScene's roots are
        //    in DDOL, LoadScene(Single) just unloads whatever the
        //    current non-DDOL scene is (an empty default scene in
        //    our case) and brings MainMenu in. GameScene stays put
        //    in DDOL, hidden but ready for Continue.
        gameObject.SetActive(false);
        if (GameSession.Instance != null)
        {
            GameSession.Instance.DeactivateHost();
        }
        SceneManager.LoadScene(_mainMenuSceneName, LoadSceneMode.Single);
    }
}
