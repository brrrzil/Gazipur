using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Round 82: full-screen mask overlay shown when the
/// player has bought a mask from a trader and walks
/// into a Danger zone. The mask is a PNG with
/// transparent eye holes (the user has the
/// artwork); the fog layer above the mask is a
/// second image that pulses between two alpha
/// values to give a 'breathing through the mask'
/// effect.
///
/// Component auto-bootstraps on first scene load
/// (same [RuntimeInitializeOnLoadMethod +
/// AfterSceneLoad] pattern as FpsCounter and
/// DebugCheat), so no scene / prefab wiring is
/// needed. The component builds its own Canvas
/// (Screen Space - Overlay, sortingOrder 100),
/// its own Image for the mask, and its own Image
/// for the fog. The mask / fog sprites are loaded
/// from the 'Resources/' folder by name:
/// 'MaskImage.png' and 'FogImage.png'. The user
/// only needs to drop the two PNGs into
/// Assets/Resources/ with the correct names
/// (Texture Type = Sprite (2D and UI), Alpha
/// Is Transparency on) and the overlay will
/// find them on the first frame.
///
/// Public API:
///   Show() - fade the canvas in over
///     _fadeDuration seconds, start the fog
///     pulse.
///   Hide() - fade the canvas out, stop the
///     fog pulse.
/// Both are safe to call multiple times; the
/// DOTween fade tween is short-circuited and
/// the fog tween is .Kill()-ed before being
/// restarted, so there is no accumulating
/// tween leak if the player runs in and out
/// of the danger zone rapidly.
/// </summary>
public class MaskOverlay : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        // Round 75/76 lesson: a MonoBehaviour on its
        // own is useless (Awake never fires), so
        // DontDestroyOnLoad + auto-create is the
        // safe pattern for cross-scene debug /
        // UI helpers. The same pattern is used by
        // FpsCounter.cs (F3 fps overlay) and
        // DebugCheat.cs (P money cheat).
        if (FindFirstObjectByType<MaskOverlay>() != null) return;
        var go = new GameObject("[MaskOverlay]");
        DontDestroyOnLoad(go);
        go.AddComponent<MaskOverlay>();
    }

    [SerializeField] private float _fadeDuration = 0.4f;
    // Fog pulses between _fogMinAlpha and
    // _fogMaxAlpha over _fogCycle seconds
    // (full ping-pong). The default values
    // produce a subtle 'breathing' effect:
    // 0.10 -> 0.30 -> 0.10 over 3 seconds.
    [SerializeField] private float _fogMinAlpha = 0.10f;
    [SerializeField] private float _fogMaxAlpha = 0.30f;
    [SerializeField] private float _fogCycle = 3f;

    private CanvasGroup _canvasGroup;
    private Image _maskImage;
    private Image _fogImage;
    private Tween _fadeTween;
    private Tween _fogTween;

    private void Awake()
    {
        BuildUI();
        // Start hidden.
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        if (_fogImage != null)
        {
            // Fog initial alpha is the min so the
            // first Yoyo loop starts from a known
            // value. (DOTween's Yoyo loop holds
            // the start value for the first half
            // of the cycle; setting the colour
            // here keeps the first cycle visually
            // identical to subsequent cycles.)
            var c = _fogImage.color;
            c.a = _fogMinAlpha;
            _fogImage.color = c;
        }
    }

    private void BuildUI()
    {
        // ----- Canvas -----
        var canvasGo = new GameObject("MaskCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // sort order 100 keeps the overlay above
        // every other Canvas in the scene (the
        // game UI Canvas is at sort order 0 by
        // default and the trade panel / settings
        // panel Canvas are also at sort order 0
        // or 1). The mask must always be on top
        // so the fog pulse is not occluded by
        // the trade panel.
        canvas.sortingOrder = 100;
        // CanvasScaler is required by the UI
        // system for the RectTransform
        // stretching below to work correctly
        // at any screen resolution.
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // GraphicRaycaster is required on a
        // Canvas that hosts Image components.
        // The mask does not need to receive
        // raycasts (the user is not clicking
        // through the mask), but the
        // raycaster has to be present or the
        // Image refuses to render in some
        // Unity versions. We disable
        // raycastTarget on the Images
        // themselves so the raycaster is a
        // no-op at runtime.
        canvasGo.AddComponent<GraphicRaycaster>();

        // ----- Mask Image -----
        var maskGo = new GameObject("MaskImage");
        maskGo.transform.SetParent(canvasGo.transform, false);
        _maskImage = maskGo.AddComponent<Image>();
        _maskImage.raycastTarget = false;
        // Stretch the image to the full canvas
        // so the eye holes line up with the
        // centre of the screen regardless of
        // aspect ratio. The user's mask
        // artwork already has transparent
        // pixels for the eye holes, so no
        // per-eye Image is needed; the
        // transparent alpha in the PNG is what
        // shows the game world through the
        // mask.
        var maskRect = _maskImage.rectTransform;
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.offsetMin = Vector2.zero;
        maskRect.offsetMax = Vector2.zero;
        // Resources.Load returns null if the
        // asset is missing or not built into
        // a Resources folder. We log a
        // one-line warning if the sprite is
        // missing rather than throwing -
        // debug helpers should never break a
        // build over a missing art asset.
        var maskSprite = Resources.Load<Sprite>("MaskImage");
        if (maskSprite != null)
            _maskImage.sprite = maskSprite;
        else
            Debug.LogWarning("[MaskOverlay] 'Resources/MaskImage.png' not found. " +
                "Drop the mask sprite into Assets/Resources/ with the name 'MaskImage' " +
                "(Texture Type = Sprite (2D and UI), Alpha Is Transparency on).");

        // ----- Fog Image -----
        var fogGo = new GameObject("FogImage");
        fogGo.transform.SetParent(canvasGo.transform, false);
        _fogImage = fogGo.AddComponent<Image>();
        _fogImage.raycastTarget = false;
        // Place the fog above the mask in the
        // hierarchy (it is the second child
        // of canvasGo) so the fog renders on
        // top of the mask artwork. The fog
        // sprite is optional - if the user
        // does not provide one the fog
        // component simply renders a flat
        // tinted quad, which still produces
        // a usable 'breathing' effect via
        // alpha pulsing.
        var fogRect = _fogImage.rectTransform;
        fogRect.anchorMin = Vector2.zero;
        fogRect.anchorMax = Vector2.one;
        fogRect.offsetMin = Vector2.zero;
        fogRect.offsetMax = Vector2.zero;
        var fogSprite = Resources.Load<Sprite>("FogImage");
        if (fogSprite != null)
            _fogImage.sprite = fogSprite;
        // Tint the fog white so a missing
        // FogImage still produces a milky
        // overlay (the alpha is what makes
        // it look like fog).
        _fogImage.color = new Color(1f, 1f, 1f, _fogMinAlpha);

        // ----- CanvasGroup -----
        // One CanvasGroup on the Canvas root
        // controls the alpha of both images
        // at once. DOTween's DOFade on
        // CanvasGroup.alpha animates the
        // whole tree in a single property
        // write per frame, which is cheaper
        // than animating two Image alphas.
        _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
    }

    public void Show()
    {
        if (_canvasGroup == null) return;
        // Kill any in-flight fade so rapid
        // Show / Hide / Show sequences do
        // not stack tweens (which would
        // cause DOTween to throw on the
        // second .DOFade call and would
        // leave the alpha at an
        // intermediate value).
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(1f, _fadeDuration);
        StartFog();
    }

    public void Hide()
    {
        if (_canvasGroup == null) return;
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(0f, _fadeDuration)
            // Hide() is the last call when the
            // player leaves the danger zone, so
            // stop the fog pulse on the same
            // frame the fade starts. The fog
            // tween is .Kill()-ed inside
            // StopFog() so it does not fight
            // the fade-out.
            .OnComplete(StopFog);
        // If the fade is interrupted by a new
        // Show() before this tween completes,
        // .Kill() above is enough - the
        // OnComplete callback does not fire
        // on a killed tween, so StopFog() is
        // not called in that case (the fog
        // continues to run, which is what we
        // want - Show() calls StartFog()
        // anyway).
    }

    private void StartFog()
    {
        if (_fogImage == null) return;
        StopFog();
        // Yoyo loop: animate alpha from
        // _fogMinAlpha to _fogMaxAlpha over
        // _fogCycle / 2 seconds, then back
        // to _fogMinAlpha over the next
        // _fogCycle / 2 seconds, looping
        // forever until StopFog() kills
        // the tween. Ease.InOutSine makes
        // the pulse feel like slow
        // breathing rather than a
        // mechanical blink.
        var startColor = _fogImage.color;
        startColor.a = _fogMinAlpha;
        _fogImage.color = startColor;
        _fogTween = _fogImage.DOFade(_fogMaxAlpha, _fogCycle / 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopFog()
    {
        _fogTween?.Kill();
        _fogTween = null;
    }
}
