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
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (!_inventory.HaveTools.Contains(EnumData.ToolsType.mask) &&
            other.GetComponent<PlayerMovement>())
        {
            _inZone = false;
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
