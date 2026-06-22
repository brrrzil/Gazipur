using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

public class InventoryCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IDropHandler, IEndDragHandler, IPointerClickHandler
{
    public bool IsReady { get; private set; }
    public ItemData Item { get; private set; }
    public int Count { get; private set; }
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Text _countText;
    [Inject] private ItemsManager _itemsManager;
    [Inject] private DataManager _data;
    [Inject] private MarketManager _market;
    [Inject] private Inventory _inventory;
    public void SetReady(bool ready) => IsReady = ready;
    private Rect _rect;
    private void Start()
    {
        _rect = _itemIcon.rectTransform.rect;
    }
    public int AddItem(ItemData item, int count)
    {
        Item = item;
        _itemIcon.enabled = true;
        _itemIcon.sprite = Item.Icon;
        int remains = Mathf.Max((Count + count) - item.MaxInInventoryCell, 0);
        Count = Mathf.Min(Item.MaxInInventoryCell, Count + count);
        _countText.text = Count.ToString();
        _inventory.ChangeCellState(this);
        return remains;
    }
    public void RemoveItem()
    {
        Item = null;
        _itemIcon.enabled = false;
        Count = 0;
        _countText.text = "";
        _inventory.ChangeCellState(this);
    }
    public void RemoveItem(int count)
    {
        if(count == Count)
        {
            RemoveItem();
            return;
        }
        Count -= count;
        _countText.text = Count.ToString();
        _inventory.ChangeCellState(this);
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!Item) return;
        _itemIcon.transform.SetParent(transform.parent);
        // (Removed redundant `transform.position = transform.position` line that
        // was immediately overwritten by the next statement.)
        _itemIcon.transform.position = eventData.position;
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!Item) return;
        _itemIcon.transform.position = eventData.position;
    }

    public void OnDrop(PointerEventData eventData)
    {
        InventoryCell source;
        if (source = eventData.pointerDrag.GetComponent<InventoryCell>())
        {
            if (source.Item == null)
                return;

            int sourceCount = source.Count;
            var sourceItem = source.Item;

            if (Item == sourceItem || !Item)
            {
                // Stack or move: fill this cell from the source.
                int rem = AddItem(sourceItem, sourceCount);
                // rem = items that didn't fit in this cell. Leave them in the
                // source cell so nothing is lost.
                source.RemoveItem(sourceCount - rem);
            }
            else
            {
                // Swap: only proceed if BOTH items fit in the other cell.
                // BUGFIX (M3): the old code unconditionally moved the contents
                // of one cell into the other via AddItem, which silently drops
                // any overflow past MaxInInventoryCell. If a swap would lose
                // items, refuse it.
                if (Count <= sourceItem.MaxInInventoryCell
                    && sourceCount <= Item.MaxInInventoryCell)
                {
                    source.RemoveItem();
                    source.AddItem(Item, Count);
                    RemoveItem();
                    AddItem(sourceItem, sourceCount);
                }
            }
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        _inventory.ShowInfoPanel(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _itemIcon.transform.parent = transform;
        _itemIcon.transform.position = transform.position;
    }
}
