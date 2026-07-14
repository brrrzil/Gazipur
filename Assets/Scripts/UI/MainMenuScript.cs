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


    private void OnStartGame() { SceneManager.LoadSceneAsync(1); }
    private void OnOpenSettings() { buttonPanel.SetActive(false); settingsPanel.SetActive(true); authorsPanel.SetActive(false); }
    private void OnOpenAuthors() { buttonPanel.SetActive(false); settingsPanel.SetActive(false); regardsPanel.SetActive(false); authorsPanel.SetActive(true); }
    private void OnOpenRegards() { buttonPanel.SetActive(false); settingsPanel.SetActive(false); authorsPanel.SetActive(true); regardsPanel.SetActive(true); }
    private void OnBack() { buttonPanel.SetActive(true); settingsPanel.SetActive(false); authorsPanel.SetActive(false); }
    private void OnBackRegards() { buttonPanel.SetActive(true); settingsPanel.SetActive(false); regardsPanel.SetActive(false); authorsPanel.SetActive(true); }
    private void OnExit() { Application.Quit(); }
}