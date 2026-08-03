using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Round 82 (v3): helper component that fades a
/// pre-existing Canvas in / out and pulses a
/// second Image as a 'breathing through the mask'
/// fog effect.
///
/// v3 redesign: the v1 / v2 versions of this
/// script auto-bootstrapped a Canvas + Image + Image
/// (mask + fog) from Resources.Load on the first
/// scene load. The user has instead set up the
/// Canvas and Image MANUALLY in the Editor (the
/// 'Panel_Gas_Mask.png' sprite is wired into an
/// Image on a Canvas in GameScene.unity, not into
/// a Resources folder), so the auto-bootstrap
/// path was creating a SECOND Canvas (with a
/// white fallback sprite, since the artwork was
/// in Sprites/ not Resources/) that did not match
/// the user's Editor setup. This v3 keeps all the
/// runtime behaviour (Show / Hide / fog pulse) but
/// drops the auto-bootstrap and expects the user
/// to wire up [SerializeField] references in the
/// Inspector. The benefit is the user controls
/// the UI exactly the way they want (no magic
/// auto-created GameObjects fighting the scene
/// hierarchy), at the cost of a few Inspector
/// drags.
///
/// What the user does in the Editor (one-time
/// setup, 6 steps):
///   1. Open the Canvas in GameScene.unity that
///      already holds the 'Panel_Gas_Mask' Image.
///   2. Select the Canvas root GameObject. In
///      the Inspector, click 'Add Component' and
///      add a CanvasGroup. Tick the boxes for
///      'Interactable' and 'Blocks Raycasts' OFF
///      (the mask is a visual-only overlay, it
///      must not eat clicks intended for the
///      game UI). Set 'Alpha' to 0 so the mask
///      starts hidden.
///   3. Select the 'Image' child of the Canvas
///      (the one whose Source Image is
///      Panel_Gas_Mask). In the RectTransform,
///      set the four anchor handles to stretch
///      the image to the full screen:
///        - Anchor Min: 0, 0
///        - Anchor Max: 1, 1
///        - Offset Min: 0, 0
///        - Offset Max: 0, 0
///      This is the step the user missed in v2
///      (the image was 20x20 at offset (150, 0),
///      which is why the mask did not appear on
///      the face). With full-screen stretch the
///      transparent eye holes in
///      Panel_Gas_Mask.png line up with the
///      centre of the screen at any aspect
///      ratio.
///   4. (Optional) Duplicate the 'Image' child
///      to create a second 'Fog' Image. Set
///      its colour to white and alpha 0.10. This
///      is the layer that pulses via DOTween
///      Yoyo to give the breathing effect. If
///      the user skips this step, the fog path
///      silently no-ops (no error).
///   5. Create an empty GameObject named
///      'MaskOverlay' (or any name) as a child
///      of the Canvas, and add the
///      'MaskOverlay' component to it. Drag:
///        - the mask Image into the 'Mask
///          Image' field
///        - the fog Image (or leave empty) into
///          the 'Fog Image' field
///        - the Canvas's CanvasGroup into the
///          'Canvas Group' field
///   6. Save the scene. The DangerZone.cs
///      OnTriggerEnter / OnTriggerExit calls
///      (committed in round 82) will then
///      invoke MaskOverlay.Show() / Hide() on
///      the user-wired component.
///
/// The DangerZone side of the integration is
/// unchanged from round 82: the script calls
/// 'FindFirstObjectByType&lt;MaskOverlay&gt;()' from
/// its OnTriggerEnter and OnTriggerExit (the
/// round 79 pattern; round 77 was the lesson
/// that [Inject] on a component that is not
/// bound in any installer triggers a
/// ZenjectException at scene start). When the
/// user has wired up MaskOverlay on a
/// GameObject in the scene, the lookup
/// succeeds; if the user has not, the lookup
/// returns null and the DangerZone silently
/// no-ops (no error, no NRE).
/// </summary>
public class MaskOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _maskImage;
    // Fog image is OPTIONAL. If left null in
    // the Inspector, the breathing effect is
    // simply skipped (Show / Hide still work
    // for the mask fade). This makes the
    // component usable with just the mask
    // image, which is the minimum the user
    // needs.
    [SerializeField] private Image _fogImage;

    [SerializeField] private float _fadeDuration = 0.4f;
    [SerializeField] private float _fogMinAlpha = 0.10f;
    [SerializeField] private float _fogMaxAlpha = 0.30f;
    [SerializeField] private float _fogCycle = 3f;

    private Tween _fadeTween;
    private Tween _fogTween;

    private void Start()
    {
        // Initial state: hidden. The user
        // already set the CanvasGroup alpha
        // to 0 in the Editor (per step 2 of
        // the setup notes), but the runtime
        // reasserts the value so a missing
        // Inspector setting is not a
        // showstopper. interactable /
        // blocksRaycasts are also forced off
        // here so the mask never eats clicks
        // even if the user leaves them on in
        // the Inspector.
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        if (_fogImage != null)
        {
            var c = _fogImage.color;
            c.a = _fogMinAlpha;
            _fogImage.color = c;
        }
    }

    public void Show()
    {
        if (_canvasGroup == null) return;
        // Kill any in-flight fade so rapid
        // Show / Hide / Show sequences do
        // not stack tweens.
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(1f, _fadeDuration);
        StartFog();
    }

    public void Hide()
    {
        if (_canvasGroup == null) return;
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(0f, _fadeDuration)
            .OnComplete(StopFog);
    }

    private void StartFog()
    {
        if (_fogImage == null) return;
        StopFog();
        // The 'breathing' effect: fog alpha
        // Yoyos between _fogMinAlpha and
        // _fogMaxAlpha over _fogCycle
        // seconds (each leg is _fogCycle /
        // 2). Ease.InOutSine makes the
        // transition feel like a slow
        // breath rather than a mechanical
        // blink.
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
