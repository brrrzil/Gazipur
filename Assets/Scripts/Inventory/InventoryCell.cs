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
    public void SetReady(bool ready) => IsReady = ready;
    public int AddItem(ItemData item, int count)
    {
        Item = item;
        _itemIcon.enabled = true;
        _itemIcon.sprite = Item.Icon;
        int remains = Mathf.Max((Count + count) - item.MaxInInventoryCell, 0);
        Count = Mathf.Min(Item.MaxInInventoryCell, Count + count);
        _countText.text = Count.ToString();
        return remains;
    }
    public void RemoveItem()
    {
        Item = null;
        _itemIcon.enabled = false;
        Count = 0;
        _countText.text = "";
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
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!Item) return;
        _itemIcon.transform.SetParent(transform.parent);
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!Item) return;
        _itemIcon.transform.position = eventData.position;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            _itemsManager.DropItem(Item, Count, eventData.position);
            RemoveItem();
        }
        else
        {
            InventoryCell target;
            if (target = eventData.pointerDrag.GetComponent<InventoryCell>())
            {
                 int cnt = target.AddItem(Item, Count);
                if (cnt == 0) RemoveItem();
                else
                {
                    Count = cnt;
                    _countText.text = Count.ToString();                    
                }
            }
        }        
    }

    public void OnPointerClick(PointerEventData eventData)
    {
       if(_data.gameMode== EnumData.GameMode.market && 
            eventData.button == PointerEventData.InputButton.Left)
        {

        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _itemIcon.transform.position = transform.position;
        _itemIcon.transform.parent = transform;
    }
}
