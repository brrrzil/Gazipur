using System.Collections;
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

    // (round 51) Preload GameScene in the background while the player is
    // on the main menu, so the actual transition on Start click is
    // instant. Uses the same allowSceneActivation=false pattern that
    // the deleted round-28 Preloader used, but implemented inside
    // MainMenu itself so we do not need a separate Preloader scene
    // (which caused AudioListener double-ups and Camera NREs in the
    // round 28-32 attempt).
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private GameObject spinner; // optional Inspector hook; created at runtime if null
    private AsyncOperation _gameSceneOp;
    private bool _sceneReady;

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

        // Start disabled: button is inert until the preload coroutine
        // reports the scene is ready (progress >= 0.9).
        startButton.interactable = false;
        if (spinner == null) spinner = BuildRuntimeSpinner();
        if (spinner != null) spinner.SetActive(true);

        StartCoroutine(LoadGameSceneAsync());
    }

    private IEnumerator LoadGameSceneAsync()
    {
        // Single mode: when allowSceneActivation flips to true later,
        // Unity unloads MainMenu and activates GameScene in one step.
        // 0.9 is the loadSceneAsync 'asset loading done, awaiting
        // activation' threshold. We stop the coroutine at 0.9 so the
        // rest (0.9 -> 1.0) is the actual scene activation, which is
        // what OnStartGame triggers.
        _gameSceneOp = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
        if (_gameSceneOp == null)
        {
            // Scene not in Build Settings or some other build error.
            // Re-enable the start button so the user is not stuck, and
            // let OnStartGame fall back to a plain LoadSceneAsync.
            Debug.LogError($"[MainMenuScript] LoadSceneAsync('{gameSceneName}') returned null. Check Build Settings.");
            startButton.interactable = true;
            if (spinner != null) spinner.SetActive(false);
            yield break;
        }
        _gameSceneOp.allowSceneActivation = false;
        while (_gameSceneOp.progress < 0.9f)
        {
            yield return null;
        }
        _sceneReady = true;
        startButton.interactable = true;
        if (spinner != null) spinner.SetActive(false);
    }

    private GameObject BuildRuntimeSpinner()
    {
        // Build a minimal spinner if the user did not wire one in the
        // Inspector. We use the built-in UISprite 'Knob' so no project
        // asset reference is needed, and we parent it under whichever
        // Canvas lives in the MainMenu scene. raycastTarget=false so
        // the spinner does not block the Start button if the layouts
        // overlap. Rotation is driven by Update() in OnGUI-free
        // fashion via a lightweight MonoBehaviour added below.
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;
        var go = new GameObject("Spinner (runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(64f, 64f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.85f);
        go.AddComponent<SpinnerRotator>();
        return go;
    }

    private void OnStartGame()
    {
        // Normal path: just flip the activation flag and Unity
        // finishes the load synchronously this frame.
        if (_gameSceneOp != null && !_sceneReady)
        {
            // User clicked before the coroutine reported ready
            // (should not happen because interactable=false, but
            // guard anyway). Force-complete by allowing activation.
            _gameSceneOp.allowSceneActivation = true;
            return;
        }
        if (_gameSceneOp != null)
        {
            _gameSceneOp.allowSceneActivation = true;
            return;
        }
        // Fallback: scene preload never started (very early click
        // before Start() finished, or null AsyncOperation). Plain
        // LoadSceneAsync by name so the user is not stuck.
        SceneManager.LoadSceneAsync(gameSceneName);
    }
    private void OnOpenSettings() { buttonPanel.SetActive(false); settingsPanel.SetActive(true); authorsPanel.SetActive(false); }
    private void OnOpenAuthors() { buttonPanel.SetActive(false); settingsPanel.SetActive(false); regardsPanel.SetActive(false); authorsPanel.SetActive(true); }
    private void OnOpenRegards() { buttonPanel.SetActive(false); settingsPanel.SetActive(false); authorsPanel.SetActive(true); regardsPanel.SetActive(true); }
    private void OnBack() { buttonPanel.SetActive(true); settingsPanel.SetActive(false); authorsPanel.SetActive(false); }
    private void OnBackRegards() { buttonPanel.SetActive(true); settingsPanel.SetActive(false); regardsPanel.SetActive(false); authorsPanel.SetActive(true); }
    private void OnExit() { Application.Quit(); }
}

// (round 51) Tiny companion script: rotates the runtime spinner at
// a constant 180 deg/sec around Z. Kept in the same file as
// MainMenuScript so the user does not need to wire an extra
// MonoBehaviour in the Inspector; the AddComponent above references
// this type by name.
public class SpinnerRotator : MonoBehaviour
{
    void Update() { transform.Rotate(0f, 0f, -180f * Time.deltaTime); }
}