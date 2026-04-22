using UnityEngine;
using Zenject;

public class Inventory : MonoBehaviour
{
    [field: SerializeField] public float Capacity { get; private set; }
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private InventoryCell[] _cells;
    [Inject] DataManager _data;
    private void Start()
    {
        Control.OnOpenInventory += () =>
        {
            _inventoryPanel.SetActive(!_inventoryPanel.activeSelf);
            Cursor.lockState =_inventoryPanel.activeSelf? CursorLockMode.None: CursorLockMode.Locked;
        };
    }
    public int AddItem(ItemData item, int count)
    {
        float weight = GetWeight();
        float cap = Capacity - weight;
        int res = 0;
        if (item.Weight * count > cap)
        {
            res = count - (int)(cap / item.Weight);
            count = (int)(cap / item.Weight);
        }
        foreach (var c in _cells)
        {            
            //if (!c.IsReady) continue;

            if (c.Item == item)
                count = c.AddItem(item, count);

            if (count == 0) break;
        }

        if (count != 0)
        {
            foreach (var c in _cells)
            {
                //if (!c.IsReady) continue;

                if (c.Item == null)
                    count = c.AddItem(item, count);
                if (count == 0) break;
            }
        }
        return count>res?count:res;
    }
    public float GetWeight()
    {
        float res = 0;
        foreach (var c in _cells)
        {
            if (c.Item)
            {
                res += c.Item.Weight * c.Count;
            }
        }
        return res;
    }
}
