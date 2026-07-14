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
        BuildUI();

        // (round 32) Prefer the main-menu's choice over the default field
        // value. If MainMenu wrote a target name into PlayerPrefs, use it.
        // Otherwise fall back to _nextSceneName (which defaults to "MainMenu",
        // so a cold start — Preloader is the boot scene and PlayerPrefs is
        // empty — still routes the player into the menu).
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
        // (round 32) Image needs an explicit sprite to render at runtime;
        // a fresh GameObject with typeof(Image) does not get the editor's
        // default "UI/Skin/Background" sprite. We share a single 1x1 white
        // sprite across all background/fill images we create here.
        var bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImage = bgGO.GetComponent<Image>();
        bgImage.sprite = GetWhiteSprite();
        bgImage.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Title text — "Загрузка..." (round 32: was "Loading...")
        _statusText = CreateText("Status", canvasGO.transform,
            "Загрузка...", 48, TextAnchor.MiddleCenter,
            new Vector2(0, 80), new Vector2(600, 80));

        // Progress bar background
        var barBGGO = new GameObject("ProgressBarBG", typeof(Image));
        barBGGO.transform.SetParent(canvasGO.transform, false);
        var barBGImage = barBGGO.GetComponent<Image>();
        barBGImage.sprite = GetWhiteSprite();
        barBGImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var barBGRT = barBGGO.GetComponent<RectTransform>();
        barBGRT.anchorMin = new Vector2(0.5f, 0.5f);
        barBGRT.anchorMax = new Vector2(0.5f, 0.5f);
        barBGRT.pivot = new Vector2(0.5f, 0.5f);
        barBGRT.sizeDelta = new Vector2(800, 30);
        barBGRT.anchoredPosition = new Vector2(0, -40);

        // Progress bar fill — Filled Image, fillAmount driven by SliderProxy
        var fillGO = new GameObject("Fill", typeof(Image));
        fillGO.transform.SetParent(barBGGO.transform, false);
        var fillImage = fillGO.GetComponent<Image>();
        fillImage.sprite = GetWhiteSprite();
        fillImage.color = new Color(0.3f, 0.7f, 1f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // (round 32) Percent text below the bar — was missing entirely
        // (only _statusText was created before). The progress percentage
        // is informative and helps the player see something is happening
        // when the fill bar is hard to read.
        _progressText = CreateText("Progress", canvasGO.transform,
            "0%", 32, TextAnchor.MiddleCenter,
            new Vector2(0, -100), new Vector2(400, 60));

        _progressBar = CreateSliderProxy(fillImage);
    }

    // (round 32) Shared 1x1 white sprite. Unity's Image component doesn't
    // render without a sprite, and a runtime-created Image has none assigned
    // (the default UI/Skin sprite is added by the editor's Reset menu, not
    // the constructor). We use Texture2D.whiteTexture (always present) and
    // wrap it in a Sprite once.
    private static Sprite _whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
        }
        return _whiteSprite;
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

        // (round 32) Guard: if the target IS the active scene (e.g. cold
        // start with an empty PlayerPrefs and the Preloader scene is the
        // boot scene and we somehow routed to "Preloader" again, or any
        // other self-target), don't try to reload the active scene. Unity
        // considers this undefined behaviour — the load can hang or assert.
        if (_nextSceneName == SceneManager.GetActiveScene().name)
        {
            Debug.LogWarning($"[Preloader] Target '{_nextSceneName}' is already the active scene; skipping reload.");
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(_nextSceneName);
        if (op == null)
        {
            Debug.LogError($"[Preloader] LoadSceneAsync returned null for '{_nextSceneName}'. " +
                           "Is the scene in Build Settings?");
            yield break;
        }
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
