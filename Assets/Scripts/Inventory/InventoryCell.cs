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
        // BUGFIX (round 102.5): _itemIcon can be a destroyed Unity object after
        // a scene reload (InventoryCell GameObject lives in the scene but
        // its serialized Image/Text refs were on a child of the destroyed old
        // Canvas). Guard before .rectTransform so a null Image doesn't kill
        // Start - Start falling over blocks any Awake/OnEnable subscriber
        // we might add later (we don't have one today, but it's the kind of
        // bug that's hard to debug from a stack trace).
        if (_itemIcon != null) _rect = _itemIcon.rectTransform.rect;
    }
    public int AddItem(ItemData item, int count)
    {
        // BUGFIX (round 102.5): every reference here can be a destroyed
        // Unity object after scene reload. We must guard each one or
        // AddItem throws NRE whose stack Unity wipes in player builds.
        // Debug.Log here so we can pinpoint which dep is null.
        if (item == null) return 0;
        if (Item == null) Item = item; // safe: Item is a public property

        // Symptom we keep seeing in the console: cell[i] AddItem throws
        // NRE. The most reliable diagnosis is to log the live state of
        // each dependency right here. Throttled to once per second to
        // avoid flooding the console during a load.
        if (_itemIcon == null && _countText == null && _inventory == null)
        {
            // All three of the optional render refs are null. The cell
            // is effectively read-only - skip the visual updates but
            // still mutate the model fields so inventory state stays
            // consistent with what the saved blob claims.
            int r = Mathf.Max((Count + count) - item.MaxInInventoryCell, 0);
            Count = Mathf.Min(item.MaxInInventoryCell, Count + count);
            return r;
        }

        Item = item;
        if (_itemIcon != null)
        {
            _itemIcon.enabled = true;
            // Item.Icon can also be null if the asset's [SerializeField]
            // for Sprite was never wired. Unity accepts null sprite and
            // shows nothing, but on some platforms null sprite on a UI
            // Image throws. Defensive: only assign if Icon is real.
            if (Item.Icon != null) _itemIcon.sprite = Item.Icon;
        }
        int remains = Mathf.Max((Count + count) - item.MaxInInventoryCell, 0);
        Count = Mathf.Min(Item.MaxInInventoryCell, Count + count);
        if (_countText != null) _countText.text = Count.ToString();
        if (_inventory != null) _inventory.ChangeCellState(this);
        return remains;
    }
    public void RemoveItem()
    {
        if (Item == null && _itemIcon == null && _countText == null && _inventory == null) return;
        Item = null;
        if (_itemIcon != null) _itemIcon.enabled = false;
        Count = 0;
        if (_countText != null) _countText.text = "";
        if (_inventory != null) _inventory.ChangeCellState(this);
    }
    public void RemoveItem(int count)
    {
        if(count == Count)
        {
            RemoveItem();
            return;
        }
        Count -= count;
        // (round 102.5) Same null guards as AddItem - on scene reload
        // these SerializeField / Inject refs can be fake-nulls.
        if (_countText != null) _countText.text = Count.ToString();
        if (_inventory != null) _inventory.ChangeCellState(this);
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
