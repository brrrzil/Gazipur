using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Zenject;

public class MainMenuScript : MonoBehaviour
{
    [SerializeField] private Button startButton, settingsButton, authorsButton, regardsButton, backSettingsButton, backAuthorsButton, backRegardsButton, exitButton;
    [SerializeField] private GameObject settingsPanel, authorsPanel, buttonPanel, regardsPanel;

    void Start()
    {
        startButton.onClick.AddListener(OnStartGame);
        settingsButton.onClick.AddListener(OnOpenSettings);
        authorsButton.onClick.AddListener(OnOpenAuthors);
        regardsButton.onClick.AddListener(OnOpenRegards);
        backSettingsButton.onClick.AddListener(OnBack);
        backAuthorsButton.onClick.AddListener(OnBack);
        backRegardsButton.onClick.AddListener(OnBackRegards);
        exitButton.onClick.AddListener(OnExit);

        buttonPanel.SetActive(true);
        settingsPanel.SetActive(false);
        authorsPanel.SetActive(false);
        regardsPanel.SetActive(false);
    }


    private void OnStartGame()
    {
        // BUGFIX (round 28): route through the Preloader scene so the
        // player sees a loading screen instead of a frozen frame while
        // the game scene deserialises. The Preloader will LoadSceneAsync
        // the real target once it's ready. Target name is read by the
        // Preloader; we pass it via PlayerPrefs to avoid spawning a
        // GameObject between the scenes just to carry a string.
        PlayerPrefs.SetString("Preloader.NextScene", "GameScene");
        PlayerPrefs.Save();
        SceneManager.LoadScene("Preloader");
    }
    private void OnOpenSettings() { buttonPanel.SetActive(false); settingsPanel.SetActive(true); authorsPanel.SetActive(false); }
    private void OnOpenAuthors() { buttonPanel.SetActive(false); settingsPanel.SetActive(false); regardsPanel.SetActive(false); authorsPanel.SetActive(true); }
    private void OnOpenRegards() { buttonPanel.SetActive(false); settingsPanel.SetActive(false); authorsPanel.SetActive(true); regardsPanel.SetActive(true); }
    private void OnBack() { buttonPanel.SetActive(true); settingsPanel.SetActive(false); authorsPanel.SetActive(false); }
    private void OnBackRegards() { buttonPanel.SetActive(true); settingsPanel.SetActive(false); regardsPanel.SetActive(false); authorsPanel.SetActive(true); }
    private void OnExit() { Application.Quit(); }
}