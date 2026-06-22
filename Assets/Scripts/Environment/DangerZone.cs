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
        if (!other.GetComponentInParent<PlayerMovement>())
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
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (!_inventory.HaveTools.Contains(EnumData.ToolsType.mask) &&
            other.GetComponentInParent<PlayerMovement>())
        {
            _inZone = false;
        }
    }
    private IEnumerator Tic()
    {
        while (_inZone)
        {
            yield return new WaitForSeconds(1f);
            // BUGFIX (M4): the player could pick up a mask while inside the
            // zone (or the inventory could change for any other reason), and
            // the old code would keep damaging them. Re-check every tick.
            if (_inventory.HaveTools.Contains(EnumData.ToolsType.mask))
            {
                _inZone = false;
                yield break;
            }
            _player.TakeDamage(_damagePerSecond);
        }
    }
}
