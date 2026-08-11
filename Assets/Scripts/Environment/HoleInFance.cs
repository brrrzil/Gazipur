using UnityEngine;
using Zenject;
using static EnumData;

public class HoleInFance : InteractObject
{
    [SerializeField] private GameObject _holeFance;
    [SerializeField] private MeshRenderer _fance;
    [SerializeField] private float _holdTaime;
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
                _holdBar.StartHold(_holdTaime);
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
        _sounds.PlayerStop();
        _holdBar.CancelHold();
        _holdBar.OnHoldComplete -= Open;
        StopInteractAnimation();
        if (_holeFance)
        {
            Instantiate(_holeFance, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}