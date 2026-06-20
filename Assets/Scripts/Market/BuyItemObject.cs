using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class BuyItemObject : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _priceText;

    [Inject] private MarketManager _market;
    [Inject] private Inventory _inventory;
    [Inject] private DataManager _data;

    private ItemData _item;

    private int _price
    {
        get
        {
            if (_item == null || _market == null) return 0;
            return (int)(_market.TraderPriceMultiplicator * _item.Price);
        }
    }

    private void OnEnable()
    {
        _buyButton.onClick.AddListener(Buy);
        if (_data != null)
            _data.onChangeMoney += RefreshAffordability;
    }

    private void OnDisable()
    {
        _buyButton.onClick.RemoveListener(Buy);
        if (_data != null)
            _data.onChangeMoney -= RefreshAffordability;
    }

    public void SetItem(ItemData item)
    {
        _item = item;
        if (_item == null)
        {
            _buyButton.interactable = false;
            _priceText.text = string.Empty;
            return;
        }

        _itemIcon.sprite = item.Icon;
        _priceText.text = _price.ToString();
        RefreshAffordability();
    }

    private void RefreshAffordability()
    {
        _buyButton.interactable = _item != null && _data != null && _data.Money >= _price;
    }

    private void Buy()
    {
        if (_item == null) return;
        if (_inventory.AddItem(_item, 1) > 0) return;
        _data.ChangeMoney(-_price);
        RefreshAffordability();
    }
}
