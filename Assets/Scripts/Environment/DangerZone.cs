using System.Collections;
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
    private GameObject _resolvedMaskRoot;

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
            if (img.sprite != null && img.sprite.name == "MaskImage")
            {
                _resolvedMaskRoot = img.gameObject;
                break;
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
        }
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
