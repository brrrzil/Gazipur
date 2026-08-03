using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[RequireComponent(typeof(MeshCollider))]
public class DangerZone : MonoBehaviour
{
    [Inject] private PlayerState _player;
    [Inject] private Inventory _inventory;
    [Inject] private DialogManager _dialog;
    [SerializeField] private int _damagePerSecond;

    // Round 82 (v4 + auto-find fallback):
    // optional [SerializeField] for the user
    // to drag the mask Canvas / Image root
    // GameObject in the Inspector. If left
    // null, Awake() does a one-time auto-
    // find of a scene Image whose
    // SourceImage sprite is 'MaskImage'
    // (i.e. the sprite imported from
    // Assets/Resources/MaskImage.png the
    // user added in 835f8c8 'Маска'). The
    // auto-find uses
    // 'FindObjectsByType<Image>
    // (FindObjectsInactive.Include,
    // FindObjectsSortMode.None)' so it
    // also picks up the user's
    // 'GasMaskImage' GameObject whose
    // m_IsActive is 0 (set in 835f8c8 to
    // make the mask start hidden). The
    // regular
    // 'FindFirstObjectByType<T>()'
    // overload would skip inactive
    // GameObjects, which would miss
    // exactly the image the user wants
    // driven.
    [SerializeField] private GameObject _maskOverlay;
    [SerializeField] private GameObject _fogOverlay;
    private GameObject _resolvedMaskRoot;
    private Image _resolvedFogImage;
    private Tween _fogTween;

    private bool _inZone;

    private void Awake()
    {
        // Round 82 (v4) auto-find fallback.
        // If the user has not dragged a
        // GameObject into the Inspector,
        // look for an Image in the scene
        // whose sprite is the one imported
        // from
        // 'Assets/Resources/MaskImage.png'.
        // The sprite's .name is 'MaskImage'
        // (matches the asset file name
        // without the extension, which is
        // what Resources.Load keys on).
        // The Image's GameObject may be
        // inactive (m_IsActive = 0) so
        // the lookup uses
        // 'FindObjectsByType<Image>
        // (FindObjectsInactive.Include,
        // FindObjectsSortMode.None)' -
        // the regular
        // 'FindFirstObjectByType<Image>()'
        // overload skips inactive
        // GameObjects, which is exactly
        // what the user does NOT want
        // here.
        if (_maskOverlay != null)
        {
            _resolvedMaskRoot = _maskOverlay;
            return;
        }
        var images = FindObjectsByType<Image>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (var img in images)
        {
            if (img.sprite != null)
            {
                // The mask is the Image
                // whose sprite is the one
                // imported from
                // 'Assets/Resources/MaskImage.png'.
                if (_resolvedMaskRoot == null && img.sprite.name == "MaskImage")
                {
                    _resolvedMaskRoot = img.gameObject;
                }
                // The fog layer is a
                // second sibling Image
                // whose sprite is the
                // one imported from
                // 'Assets/Resources/FogImage.png'
                // (or any other
                // 'FogImage' sprite
                // the user drops in).
                // The fog Image is
                // optional: if no
                // Image with sprite
                // name 'FogImage' is
                // found, the fog path
                // is a silent no-op
                // (no DOTween started,
                // no error, no NRE).
                if (_resolvedFogImage == null && img.sprite.name == "FogImage")
                {
                    _resolvedFogImage = img;
                }
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        if (!_inventory.HaveTools.Contains(EnumData.ToolsType.mask))
        {
            _dialog.Remarks.StartRemark(EnumData.RemarksType.noMask);
            _inZone = true;
            StartCoroutine(Tic());
        }
        else
        {
            _dialog.Remarks.StartRemark(EnumData.RemarksType.maskReady);
            // Round 82 (v4 + auto-find
            // fallback): enable the mask
            // overlay. Two paths are tried
            // in order:
            //   1. MaskOverlay component
            //      in the scene (if the
            //      user has set it up,
            //      this gives them the
            //      optional fade). The
            //      lookup is the round 79
            //      pattern (Unity 6
            //      FindFirstObjectByType
            //      + null-conditional).
            //   2. The auto-found
            //      '_resolvedMaskRoot'
            //      GameObject (the
            //      user's GasMaskImage
            //      or whatever they
            //      named it, found in
            //      Awake). SetActive(true)
            //      toggles the image on
            //      with no animation.
            // Path 1 takes precedence; if
            // MaskOverlay is not in the
            // scene, path 2 is used. Both
            // are null-guarded so a
            // misconfigured scene is a
            // silent no-op rather than an
            // NRE.
            var overlay = FindFirstObjectByType<MaskOverlay>();
            if (overlay != null)
            {
                overlay.Show();
            }
            else if (_resolvedMaskRoot != null && !_resolvedMaskRoot.activeSelf)
            {
                _resolvedMaskRoot.SetActive(true);
            }
            // Round 82 (v5): start the
            // 'breathing through the
            // mask' fog pulse. The fog
            // layer is a separate
            // sibling Image whose
            // sprite is the one
            // imported from
            // 'Assets/Resources/FogImage.png'
            // (or any 'FogImage'
            // sprite). The pulse is
            // a DOTween Yoyo on the
            // Image's colour alpha,
            // 0.10 <-> 0.30 over 3
            // seconds with
            // Ease.InOutSine, so the
            // fog looks like a slow
            // breath rather than a
            // mechanical blink. If
            // no fog Image is found
            // (the user has not
            // created one in the
            // scene) the lookup
            // returns null and the
            // fog path is a silent
            // no-op - no NRE, no
            // warning, just no fog.
            StartFog();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        if (!_inventory.HaveTools.Contains(EnumData.ToolsType.mask))
        {
            // Player left the danger zone without
            // a mask - the on-trigger-enter path
            // was the one that started the damage
            // tick coroutine, so this is where we
            // stop it.
            _inZone = false;
        }
        else
        {
            // Round 82 (v4 + auto-find
            // fallback): mirror of the
            // OnTriggerEnter path. Try
            // MaskOverlay.Hide() first, then
            // fall back to SetActive(false)
            // on the auto-found root. The
            // 'maskReady' character remark
            // is intentionally NOT replayed
            // here - it was an entry cue,
            // not a looping voice line.
            var overlay = FindFirstObjectByType<MaskOverlay>();
            if (overlay != null)
            {
                overlay.Hide();
            }
            else if (_resolvedMaskRoot != null && _resolvedMaskRoot.activeSelf)
            {
                _resolvedMaskRoot.SetActive(false);
            }
            // Round 82 (v5): stop the
            // fog pulse on exit so
            // the breathing does not
            // keep running when the
            // mask is hidden.
            StopFog();
        }
    }
    // Round 82 (v5): fog pulse
    // implementation. The fog is
    // a second sibling Image on
    // the same Canvas as the
    // mask. It is OPTIONAL: if
    // the user has not created
    // one (or the auto-find did
    // not match a 'FogImage'
    // sprite), the methods are
    // silent no-ops.
    private void StartFog()
    {
        if (_resolvedFogImage == null) return;
        StopFog();
        // Reset to known start
        // alpha so the first Yoyo
        // leg is identical to
        // subsequent ones.
        var c = _resolvedFogImage.color;
        c.a = 0.10f;
        _resolvedFogImage.color = c;
        // Yoyo loop: 0.10 -> 0.30
        // over 1.5s, 0.30 -> 0.10
        // over 1.5s, looping
        // forever until StopFog().
        // Ease.InOutSine makes it
        // feel like a slow breath.
        _fogTween = _resolvedFogImage.DOFade(0.30f, 1.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopFog()
    {
        if (_fogTween == null) return;
        _fogTween.Kill();
        _fogTween = null;
    }

    private IEnumerator Tic()
    {
        while (_inZone)
        {
            yield return new WaitForSeconds(1f);
            _player.TakeDamage(_damagePerSecond);
        }

    }
}
