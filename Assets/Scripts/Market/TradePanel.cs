using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class TradePanel : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Button _tradeButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private Text _curCountText;
    [SerializeField] private Text _sellCountText;
    [SerializeField] private Text _priceText;
    [SerializeField] private Slider _slider;
    [SerializeField] private GameObject _tradeCap;

    private int _price => _cell.Item.Price;
    private int _count;
    private int _sellCount;
    private InventoryCell _cell;
    [Inject] private DataManager _data;
    [Inject] private MarketManager _market;
    private void Start()
    {
        _slider.onValueChanged.AddListener(ChangeCount);
        _tradeButton.onClick.AddListener(Trade);
        _exitButton.onClick.AddListener(_market.Exit);
    }
    public void SetItem(InventoryCell cell)
    {
        if (!cell.Item) 
        {
            _tradeCap.SetActive(true);
            return;
        }
        _tradeCap.SetActive(false);
        _itemIcon.sprite = cell.Item.Icon;
        _cell = cell;
        _count = cell.Count;
        _sellCount = _count;
        _slider.minValue = 0;
        _slider.maxValue = _count;
        SetUI();
    }
    private void ChangeCount(float value)
    {
        _sellCount = (int)value;
        SetUI();
    }

    private void Trade()
    {
        _data.ChangeMoney(_sellCount * _price);
        _cell.RemoveItem(_sellCount);
        SetItem(_cell);
    }
    private void SetUI()
    {
        _tradeButton.interactable = _sellCount > 0;
        _slider.value = _sellCount;
        _priceText.text = (_sellCount * _price).ToString();
        _sellCountText.text = _sellCount.ToString();
        _curCountText.text = (_count - _sellCount).ToString();
    }
    public void Show()
    {
        _tradeCap.SetActive(true);
        gameObject.SetActive(true);
    }
}

