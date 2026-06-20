using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private float _interactableDistance = 3f;

    private InteractObject _selectObject;

    private void OnEnable()
    {
        Control.OnSelectObject += SelectObject;
        Control.OnInteractObject += InteractObject;
    }

    private void OnDisable()
    {
        Control.OnSelectObject -= SelectObject;
        Control.OnInteractObject -= InteractObject;

        if (_selectObject != null)
        {
            _selectObject.Select(false);
            _selectObject = null;
        }
    }

    private void SelectObject(InteractObject obj)
    {
        if (_selectObject != null)
            _selectObject.Select(false);

        _selectObject = obj;

        // Vector3.Distance всегда неотрицательная — Mathf.Abs избыточен.
        if (obj != null
            && Vector3.Distance(transform.position, obj.transform.position) <= _interactableDistance)
        {
            _selectObject.Select(true);
        }
    }

    private void InteractObject()
    {
        if (_selectObject != null)
        {
            _selectObject.Intearct();
        }
    }
}
