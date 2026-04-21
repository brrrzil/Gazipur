using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class MainMenuScript : MonoBehaviour
{
    [SerializeField] private Button startButton, settingsButton, authorsButton, backSettingsButton, backAuthorsButton, exitButton;
    [SerializeField] private GameObject settingsPanel, authorsPanel, buttonPanel;

    void Start()
    {
        startButton.onClick.AddListener(OnStartGame);
        settingsButton.onClick.AddListener(OnOpenSettings);
        authorsButton.onClick.AddListener(OnOpenAuthors);
        backSettingsButton.onClick.AddListener(OnBack);
        backAuthorsButton.onClick.AddListener(OnBack);
        exitButton.onClick.AddListener(OnExit);

        buttonPanel.SetActive(true);
        settingsPanel.SetActive(false);
        authorsPanel.SetActive(false);
    }

    private void OnStartGame()
    {
        SceneManager.LoadSceneAsync(1);
    }

    private void OnOpenSettings()
    {
        buttonPanel.SetActive(false);
        settingsPanel.SetActive(true);
        authorsPanel.SetActive(false);
    }

    private void OnOpenAuthors()
    {
        buttonPanel.SetActive(false);
        settingsPanel.SetActive(false);
        authorsPanel.SetActive(true);
    }

    private void OnBack()
    {
        buttonPanel.SetActive(true);
        settingsPanel.SetActive(false);
        authorsPanel.SetActive(false);
    }

    private void OnExit()
    {
        Application.Quit();
    }
}