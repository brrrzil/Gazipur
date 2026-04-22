using UnityEngine;
using System;
using UnityEngine.EventSystems;

public class Control : MonoBehaviour
{
    public static Action<Vector2> OnMouseDownInObject;
    public static Action<InteractObject> OnSelectObject;
    public static Action OnInteractObject;
    public static Action OnOpenInventory;

    private void Update()
    {
        InteractObject iObject = null;
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (iObject = hit.collider.GetComponent<InteractObject>()) { }
            }
        }        
        if (Input.GetKeyDown(KeyCode.E))
        {
            OnInteractObject?.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            OnOpenInventory?.Invoke();
        }
        OnSelectObject?.Invoke(iObject);
    }
}
