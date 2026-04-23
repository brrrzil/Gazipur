using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class BuyItemObject : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Text _priceText;
    [Inject] private MarketManager _market;
    [Inject] private Inventory _inventory;
    [Inject] private DataManager _data;
    private ItemData _item;
    private int _price => (int)(_market.TraderPriceMultiplicator * _item.Price);
    public void SetItem(ItemData item)
    {
        _item = item;
        _itemIcon.sprite = item.Icon;
        _priceText.text = _price.ToString();
        _buyButton.onClick.AddListener(Buy);
        _data.onChangeMoney += () => _buyButton.interactable = _data.Money >= _price;
    }
    private void Buy()
    {
        if (_inventory.AddItem(_item, 1) > 0)
        {
            return;
        }
        _data.ChangeMoney(-_price);
    }
}
