using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Preloader : MonoBehaviour
{
    [SerializeField] private string _nextSceneName = "MainMenu";
    [SerializeField] private string _nextScenePlayerPrefsKey = "Preloader.NextScene";

    private SliderProxy _progressBar;
    private Text _progressText;
    private Text _statusText;
    private Canvas _canvas;

    private void Awake()
    {
        // The Preloader scene is intentionally minimal — just a Camera +
        // EventSystem + this GameObject. We build the entire UI in code so
        // the scene file stays small and the project doesn't need a hand-
        // crafted .unity file with Canvas / Slider / Text wiring.
        BuildUI();

        // If the main menu set a specific target scene (it always should),
        // prefer that over the default field value. This way the same
        // Preloader can be reused for any scene transition later.
        if (PlayerPrefs.HasKey(_nextScenePlayerPrefsKey))
        {
            string fromMenu = PlayerPrefs.GetString(_nextScenePlayerPrefsKey);
            if (!string.IsNullOrEmpty(fromMenu))
                _nextSceneName = fromMenu;
        }
    }

    private void Start()
    {
        StartCoroutine(LoadNextSceneRoutine());
    }

    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("PreloaderCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = 5; // UI layer
        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100; // above everything else

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Background — solid dark color, covers full screen
        var bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.GetComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Title text "Loading..."
        _statusText = CreateText("Status", canvasGO.transform,
            "Loading...", 48, TextAnchor.MiddleCenter,
            new Vector2(0, 80), new Vector2(600, 80));

        // Progress bar background
        var barBGGO = new GameObject("ProgressBarBG", typeof(Image));
        barBGGO.transform.SetParent(canvasGO.transform, false);
        var barBGImage = barBGGO.GetComponent<Image>();
        barBGImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var barBGRT = barBGGO.GetComponent<RectTransform>();
        barBGRT.anchorMin = new Vector2(0.5f, 0.5f);
        barBGRT.anchorMax = new Vector2(0.5f, 0.5f);
        barBGRT.pivot = new Vector2(0.5f, 0.5f);
        barBGRT.sizeDelta = new Vector2(800, 30);
        barBGRT.anchoredPosition = new Vector2(0, -40);

        // Progress bar fill — using a child Image with Filled type
        var fillGO = new GameObject("Fill", typeof(Image));
        fillGO.transform.SetParent(barBGGO.transform, false);
        var fillImage = fillGO.GetComponent<Image>();
        fillImage.color = new Color(0.3f, 0.7f, 1f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // Expose the fill image as the progress bar — we update fillAmount
        // directly instead of value, because Filled Image doesn't have a
        // Slider component to drive. (A Slider would also work; using Image
        // here is simpler since we don't need interaction.)
        _progressBar = CreateSliderProxy(fillImage);
    }

    private static Text CreateText(string name, Transform parent, string content,
        int fontSize, TextAnchor alignment, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return text;
    }

    // Tiny helper so the rest of the code can use `_progressBar.value` even
    // though the underlying object is a Filled Image.
    private class SliderProxy
    {
        public Image Fill { get; }
        public float value
        {
            get => Fill.fillAmount;
            set => Fill.fillAmount = Mathf.Clamp01(value);
        }
        public SliderProxy(Image fill) { Fill = fill; }
    }
    private SliderProxy CreateSliderProxy(Image fill) => new SliderProxy(fill);

    private IEnumerator LoadNextSceneRoutine()
    {
        if (string.IsNullOrEmpty(_nextSceneName))
        {
            Debug.LogError("[Preloader] No next scene name set; aborting.");
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(_nextSceneName);
        op.allowSceneActivation = false;

        // LoadSceneAsync reports progress 0..0.9 while the scene loads,
        // then 0.9 while waiting for activation. Hold at 0.9 until we've
        // drawn at least one frame of '100%' so the user gets visible
        // feedback that loading finished.
        while (op.progress < 0.9f)
        {
            UpdateProgress(op.progress);
            yield return null;
        }

        UpdateProgress(1f);
        yield return new WaitForSeconds(0.25f); // brief pause so 100% is visible

        op.allowSceneActivation = true;

        // Clean up the key so a fresh game start doesn't keep stale data.
        PlayerPrefs.DeleteKey(_nextScenePlayerPrefsKey);
    }

    private void UpdateProgress(float p)
    {
        if (_progressBar != null) _progressBar.value = p;
        if (_progressText != null) _progressText.text = $"{(p * 100f):F0}%";
    }
}
