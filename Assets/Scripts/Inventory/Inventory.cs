using UnityEngine;
using Zenject;

public class Inventory : MonoBehaviour
{
    [field: SerializeField] public float Capacity { get; private set; }
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private InventoryCell[] _cells;

    private bool _isOpen;
    [Inject] DataManager _data;
    private void OnEnable()
    {
        Control.OnOpenInventory += TogglePanel;
    }
    private void OnDisable()
    {
        Control.OnOpenInventory -= TogglePanel;
    }
    private void TogglePanel()
    {
        ShowPanel(!_isOpen);
    }
    public int AddItem(ItemData item, int count)
    {
        // Сумки (CapacityUpgrade) не кладутся в ячейки, а просто увеличивают
        // вместимость. Можно купить несколько — каждый даст бонус.
        if (item != null && item.Type == ItemData.ItemType.CapacityUpgrade)
        {
            Capacity += item.CapacityBonus * count;
            return 0;
        }

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

        // count — сколько осталось не размещено в ячейках,
        // res — сколько не влезло по весу. Сумма — это то, что осталось
        // в ItemObject (не подобранное игроком).
        return count + res;
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
        if (_inventoryPanel != null)
        {
            _inventoryPanel.SetActive(isShow);
        }
        Cursor.lockState = isShow ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isShow;
    }
}
