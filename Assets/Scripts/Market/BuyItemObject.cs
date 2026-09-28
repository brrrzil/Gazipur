using UnityEngine;
using UnityEngine.UI;
using Zenject;
using UnityEngine.EventSystems;

public class BuyItemObject : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Text _priceText;
    [Inject] private MarketManager _market;
    [Inject] private Inventory _inventory;
    [Inject] private DataManager _data;
    [Inject] private Sounds _sound;
    private ItemData _item;
    private bool _isSingle;
    private int _price => (int)(_market.TraderPriceMultiplicator * _item.Price);

    // BUGFIX (round 17): fired when this buy object sells a BagItem. Used
    // by MarketManager to unlock the next bag in the sequence.
    public System.Action OnBagPurchased;

    // (fix/save-load-subscriptions) Cached so OnDestroy can unsubscribe
    // exactly this delegate. The previous version declared an anonymous
    // lambda inline and never removed it - every time the player opened
    // the trade panel, MarketManager.AddItem spawned a fresh BuyItemObject,
    // and each SetItem() appended another lambda to DataManager.onChangeMoney.
    // After a few Continue + trade cycles the invocation list was a stack
    // of dead MonoBehaviours; the next Money change would NRE on the first
    // destroyed one and silently swallow the rest, which is exactly the
    // 'button stops reacting after reload' symptom.
    private System.Action _onMoneyChangedHandler;

    public void OnPointerClick(PointerEventData eventData)
    {
        _inventory.ItemInfoPanel.SetPurchasableItem(_item);
    }

    public void SetItem(ItemData item, bool isSingle)
    {
        _isSingle = isSingle;
        _item = item;
        _itemIcon.sprite = item.Icon;
        _priceText.text = _price.ToString();
        _buyButton.onClick.AddListener(Buy);
        _buyButton.interactable = _data.Money >= _price;

        // (fix/save-load-subscriptions) Build the lambda once and store
        // it so OnDestroy can remove it. Re-subscribing on every SetItem
        // (market panel is rebuilt by MarketManager each session) used to
        // leave the previous delegate orphaned in DataManager.onChangeMoney.
        _onMoneyChangedHandler = () =>
        {
            // Unity null-check: scene reload (New Game / Continue reload)
            // destroys BuyItemObject while DataManager survives as a Zenject
            // scene-context service. The captured 'this' is then a destroyed
            // MonoBehaviour and _buyButton.gameObject throws NRE when this
            // lambda fires from DataManager.onChangeMoney. Guard both ends.
            if (this == null) return;
            if (_buyButton == null) return;
            _buyButton.interactable = _data.Money >= _price;
        };
        _data.onChangeMoney += _onMoneyChangedHandler;
    }

    private void OnDestroy()
    {
        // (fix/save-load-subscriptions) Symmetric teardown so the lambda
        // added in SetItem is removed when the buy object goes away.
        // Without this, every Continue -> open-trader cycle leaks one
        // delegate into DataManager.onChangeMoney.
        if (_data != null && _onMoneyChangedHandler != null)
            _data.onChangeMoney -= _onMoneyChangedHandler;
    }
    private void Buy()
    {
        // MapItem is consumed immediately on purchase - it opens the
        // minimap and is NOT added to the inventory. The player does
        // not need to keep the item in their bags: once bought, the
        // minimap is permanently unlocked for the session.
        if (_item.ItemPrefab is MapItem)
        {
            if (MapUI.Instance != null)
                MapUI.Instance.Unlock();
            if (_isSingle) gameObject.SetActive(false);
            _data.ChangeMoney(-_price);
            _sound.UIPlay(EnumData.UISound.buy);
            return;
        }

        if (_inventory.AddItem(_item, 1) > 0)
        {
            return;
        }
        if (_isSingle) gameObject.SetActive(false);
        _data.ChangeMoney(-_price);
        _sound.UIPlay(EnumData.UISound.buy);

        // BUGFIX (round 17): notify MarketManager so it can unlock the next
        // bag in the sequence (only fires when this item is a BagItem).
        if (_item.ItemPrefab is BagItem)
            OnBagPurchased?.Invoke();
    }

}
