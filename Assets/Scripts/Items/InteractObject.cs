using UnityEngine;

[RequireComponent(typeof(Outline))]
public abstract class InteractObject : MonoBehaviour
{
    private Outline _outline; 
    public void Select(bool isSelect)
    {
        _outline ??= GetComponent<Outline>();
        _outline.enabled = isSelect;
    }
    public abstract void Intearct();
}

