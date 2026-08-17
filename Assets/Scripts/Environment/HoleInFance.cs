using UnityEngine;
using Zenject;
using static EnumData;

public class HoleInFance : InteractObject
{
    [SerializeField] private GameObject _holeFence;
    [SerializeField] private MeshRenderer _fence;
    [SerializeField] private float _holdTime;
    [SerializeField] private PlayerSound _openSound;
    [SerializeField] private ToolsType _tool;
    [SerializeField] private RemarksType _remark;

    [Inject] Inventory _inventory;
    [Inject] HoldProgressBar _holdBar;
    [Inject] DialogManager _dialog;
    [Inject] Sounds _sounds;

    public override void Intearct(bool isDown)
    {

        if (_inventory.HaveTools.Contains(_tool))
        {
            if (isDown)
            {
                _holdBar.StartHold(_holdTime);
                _holdBar.OnHoldComplete += Open;
                _sounds.PlayerPlay(_openSound, false);
                PlayInteractAnimation();
            }
            else
            {
                _sounds.PlayerStop();
                _holdBar.CancelHold();
                _holdBar.OnHoldComplete -= Open;
                StopInteractAnimation();
            }
        }
        else
        {
            _dialog.Remarks.StartRemark(_remark);
        }
    }

    private void Update()
    {
        if (_holdBar != null && _holdBar.IsActive)
            KeepAnimationLockAlive();
    }

    private void Open()
    {
        //_sounds.PlayerStop();
        _holdBar.CancelHold();
        _holdBar.OnHoldComplete -= Open;
        StopInteractAnimation();
        if (_holeFence)
        {
            Instantiate(_holeFence, transform.position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning($"[HoleInFance] {gameObject.name}: _holeFence prefab is not assigned. The fence is destroyed without a 'hole' replacement. Drag a hole fence prefab onto the _holeFence field in the Inspector.");
        }
        Destroy(gameObject);
    }
}