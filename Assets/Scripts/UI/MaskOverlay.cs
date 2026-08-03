using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Round 82 (v4): minimal fade / show-hide
/// helper for the on-screen mask overlay the
/// user has set up in GameScene.unity. The
/// component does NOT create any GameObjects
/// (the user has the Canvas with the
/// 'Panel_Gas_Mask' Image in the scene and
/// wants it left exactly where it is). The
/// component does NOT load anything from
/// Resources (the artwork is in
/// Assets/Sprites/Panel_Gas_Mask.png, not
/// in a Resources folder, and the user does
/// not want it to be). The component is just
/// a Show / Hide driver: Show() turns the
/// mask Canvas on, Hide() turns it off.
///
/// The optional fade is driven by a
/// CanvasGroup the user can add to the
/// Canvas. If the CanvasGroup is wired up
/// via the Inspector, Show() / Hide() fade
/// the alpha in / out via DOTween and only
/// SetActive(false) the Canvas when the
/// fade-out finishes. If the CanvasGroup
/// is left empty in the Inspector, Show /
/// Hide are pure SetActive(true / false)
/// with no animation - which is the
/// behaviour the user described ('mask is
/// off by default, should turn on when the
/// right event fires, should not be created
/// anywhere').
///
/// The 'right event' in this project is the
/// round 82 wiring in
/// Assets/Scripts/Environment/DangerZone.cs:
/// when the player walks into a Danger zone
/// with the mask tool in
/// Inventory.HaveTools, DangerZone calls
/// 'FindFirstObjectByType&lt;MaskOverlay&gt;()'
/// and invokes Show() on it; when the
/// player walks out, DangerZone invokes
/// Hide(). The lookup is the same
/// round-79 / round-82 null-guarded pattern
/// the rest of the DangerZone side uses.
///
/// The user is expected to do the
/// following one-time setup in the Editor
/// (the 'show, don't create' rule the user
/// asked for):
///   1. The Canvas with the
///      'Panel_Gas_Mask' Image is
///      already in GameScene.unity
///      (added in commit 10803da
///      'GasMask' by AndreyN). Set
///      the Canvas's m_IsActive to 0
///      (uncheck the active toggle in
///      the Hierarchy) so the mask
///      starts hidden.
///   2. Create an empty GameObject
///      named 'MaskOverlay' anywhere
///      in the scene (a child of the
///      Canvas is fine, or a sibling
///      of the Canvas - the user
///      chooses). Add the 'MaskOverlay'
///      component to it.
///   3. In the Inspector, drag the
///      mask Canvas GameObject into
///      the 'Mask Root' field. This
///      is the reference Show / Hide
///      SetActive on. (The
///      'MaskOverlay' GameObject
///      itself is NOT the 'Mask Root'
///      - the 'Mask Root' is the
///      Canvas you want to be
///      shown / hidden.)
///   4. (Optional, for the fade
///      effect) Add a CanvasGroup
///      component to the mask Canvas
///      and drag it into the 'Canvas
///      Group' field. Set its
///      starting alpha to 0 in the
///      Inspector. If the user
///      prefers the no-fade
///      behaviour, this field can
///      stay empty and the
///      component will just
///      SetActive the Canvas
///      without any tween.
///   5. Save the scene. From this
///      point the DangerZone.cs
///      FindFirstObjectByType
///      lookup will find the
///      user-placed MaskOverlay
///      (the MaskOverlay GameObject
///      itself must be active so
///      FindFirstObjectByType can
///      find it - that is why it
///      is recommended to put
///      MaskOverlay on a separate
///      GameObject from the
///      mask Canvas, with the mask
///      Canvas as the disabled-by-
///      default 'Mask Root').
/// </summary>
public class MaskOverlay : MonoBehaviour
{
    [SerializeField] private GameObject _maskRoot;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeDuration = 0.3f;

    private Tween _fadeTween;

    public void Show()
    {
        // Round 82 v4: minimal 'turn it on'
        // behaviour. If the user wired up a
        // CanvasGroup, fade the alpha 0 -> 1
        // first and SetActive the Canvas
        // root (so the canvas + image are
        // also visible during the fade). If
        // no CanvasGroup is wired up, just
        // SetActive the Canvas on with no
        // animation.
        if (_canvasGroup != null)
        {
            if (_maskRoot != null && !_maskRoot.activeSelf)
                _maskRoot.SetActive(true);
            _fadeTween?.Kill();
            _fadeTween = _canvasGroup.DOFade(1f, _fadeDuration);
        }
        else
        {
            if (_maskRoot != null && !_maskRoot.activeSelf)
                _maskRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (_canvasGroup != null)
        {
            // Fade the alpha back to 0, then
            // SetActive(false) the canvas on
            // the OnComplete callback. The
            // .Kill() in the next Show() call
            // short-circuits the OnComplete so
            // a fast Show / Hide / Show
            // sequence does not flip the
            // canvas off under a half-faded-
            // in alpha.
            _fadeTween?.Kill();
            _fadeTween = _canvasGroup.DOFade(0f, _fadeDuration)
                .OnComplete(() =>
                {
                    if (_maskRoot != null && _maskRoot.activeSelf)
                        _maskRoot.SetActive(false);
                });
        }
        else
        {
            if (_maskRoot != null && _maskRoot.activeSelf)
                _maskRoot.SetActive(false);
        }
    }
}
