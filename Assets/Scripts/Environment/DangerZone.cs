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
            // Round 82: the player has the mask
            // tool, so show the on-screen mask
            // overlay. The overlay is a
            // full-screen Image with transparent
            // eye holes (loaded from
            // Resources/MaskImage) plus a fog
            // layer above it that pulses via
            // DOTween Yoyo. MaskOverlay
            // auto-bootstraps on first scene
            // load so we use the type lookup
            // (FindFirstObjectByType, Unity 6
            // API) instead of [Inject] - the
            // round-77 lesson was that dead
            // [Inject] fields trigger
            // ZenjectException when the binding
            // is missing, and MaskOverlay is not
            // bound in any installer in the
            // project. The null-conditional '?.'
            // handles the case where the overlay
            // has not been instantiated yet (it
            // would be, by the AfterSceneLoad
            // hook, but a defensive null-check
            // costs nothing).
            var overlay = FindFirstObjectByType<MaskOverlay>();
            if (overlay != null) overlay.Show();
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
            // Round 82: player left the danger
            // zone WITH a mask - hide the mask
            // overlay. Show() was called in the
            // OnTriggerEnter 'else' branch when
            // the player walked in, so the
            // matching Hide() goes here on the
            // way out. Same null-conditional
            // pattern as above: MaskOverlay
            // should exist by now but a defensive
            // null-check is harmless. The
            // character remark 'maskReady' is
            // intentionally NOT replayed on exit
            // - it was an entry cue, not a
            // looping voice line.
            var overlay = FindFirstObjectByType<MaskOverlay>();
            if (overlay != null) overlay.Hide();
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
