using UnityEngine;
using Zenject;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private float _interactableDistance;
    private InteractObject _selectObject;
    private bool _isSelect;
    [Inject] Control _control;
    private void Start()
    {
        _control.OnSelectObject += SelectObject;
        _control.OnHoldInteract += InteractObject;
    }
    private void SelectObject(InteractObject obj)
    {
        // If the previously-selected object went out of range (e.g. the player
        // walked away while still aiming at it), we must re-check the distance
        // every frame and drop _isSelect so the highlight goes away. The
        // previous implementation short-circuited with `if (_isSelect) return;`
        // when the object didn't change, which kept stale _isSelect alive
        // forever once set.
        if (obj != _selectObject)
        {
            if (_selectObject != null)
                _selectObject.Select(false);
            _selectObject = obj;
            _isSelect = false;
        }

        if (obj == null)
        {
            _isSelect = false;
            return;
        }

        bool inRange = Vector3.Distance(transform.position, obj.transform.position) <= _interactableDistance;
        if (inRange != _isSelect)
        {
            _isSelect = inRange;
            obj.Select(_isSelect);
        }
    }
    private void InteractObject(bool isDown)
    {
        // Also require _isSelect so we don't fire Intearct on a target that the
        // SelectObject() loop already determined was out of range. Without this
        // guard, aiming at the trader from far away (without ever being close)
        // would still call Intearct() on every E press, because the raycast
        // populates _selectObject regardless of distance.
        if (_selectObject != null && _isSelect)
        {
            _selectObject.Intearct(isDown);
        }
    }
    private void OnDestroy()
    {
        _control.OnSelectObject -= SelectObject;
        _control.OnHoldInteract -= InteractObject;
    }
}
