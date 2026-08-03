using System.Collections;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(MeshCollider))]
public class DangerZone : MonoBehaviour
{
    [Inject] private PlayerState _player;
    [Inject] private Inventory _inventory;
    [Inject] private DialogManager _dialog;
    [SerializeField] private int _damagePerSecond;

    // Round 82 (v6): the mask Canvas /
    // Image GameObject the user has
    // set up in the scene. The user
    // drags the Canvas (or the Image
    // GameObject) into this slot in
    // the Inspector and the script
    // toggles its active state on /
    // off in OnTriggerEnter /
    // OnTriggerExit. The script does
    // NOT search the scene with
    // FindObjectsByType to locate
    // the mask - the Inspector
    // reference is the only
    // source of truth, which keeps
    // the cost at zero per trigger
    // event and removes the
    // fragility of matching by
    // sprite .name (a misnamed
    // asset or an extra Image in
    // the scene would silently
    // change which Image the script
    // drives).
    [SerializeField] private GameObject _maskOverlay;

    private bool _inZone;

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
            // Round 82 (v6): the user
            // has the mask tool in
            // HaveTools, so enable the
            // mask Canvas the user
            // wired into the Inspector.
            // No Find, no auto-discovery
            // - the Inspector reference
            // is the source of truth.
            // The null-conditional guard
            // handles a misconfigured
            // Inspector (the user has
            // not dragged anything in)
            // as a silent no-op.
            if (_maskOverlay != null && !_maskOverlay.activeSelf)
                _maskOverlay.SetActive(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (!other.GetComponent<PlayerMovement>())
            return;

        if (!_inventory.HaveTools.Contains(EnumData.ToolsType.mask))
        {
            // Player left the danger zone
            // without a mask - the
            // OnTriggerEnter path was the
            // one that started the damage
            // tick coroutine, so this is
            // where we stop it.
            _inZone = false;
        }
        else
        {
            // Round 82 (v6): mirror of
            // the OnTriggerEnter path -
            // turn the mask Canvas off
            // on the way out. Same
            // null-conditional guard.
            // The 'maskReady' character
            // remark is intentionally NOT
            // replayed here - it was an
            // entry cue, not a looping
            // voice line.
            if (_maskOverlay != null && _maskOverlay.activeSelf)
                _maskOverlay.SetActive(false);
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
