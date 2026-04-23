using UnityEngine;
using Zenject;

[RequireComponent(typeof(MeshFilter))]
public class ItemObject : InteractObject
{
    [field: SerializeField] public ItemData Item { get; private set; }
    [field: SerializeField] public int Count { get; private set; }    
    [Inject] private Inventory _inventory;
    private void Start()
    {        
        if (Item)
        {
            SetData(Item, Count);
        }
    }
    public void SetData(ItemData item, int count)
    {
        Count = count;
        Item = item;
    }
    public override void Intearct()
    {
        int cnt = _inventory.AddItem(Item, Count);
        if (cnt == 0)
        {
            Destroy(gameObject);
        }
        else
        {
            Count = cnt;
        }
    }
}
