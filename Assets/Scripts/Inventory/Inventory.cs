using UnityEngine;
using Zenject;

public class Inventory : MonoBehaviour
{
    [field: SerializeField] public float Capacity { get; private set; }
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private InventoryCell[] _cells;

    private bool _isOpen;
    [Inject] DataManager _data;
    private void Start()
    {
        Control.OnOpenInventory += () => ShowPanel(!_isOpen);
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
    public void ShowPanel(bool isShow)
    {
        _isOpen = isShow;
        _inventoryPanel.SetActive(isShow);
        Cursor.lockState = isShow ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
