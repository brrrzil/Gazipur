using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class TradePanel : MonoBehaviour
{
    [SerializeField] private Button TradeButton;
    [SerializeField] private Text _curCountText;
    [SerializeField] private Text _sellCountText;
    [SerializeField] private Text _priceText;
    [SerializeField] private Slider _slider;

    private int _price => _cell.Item.Price;
    private int _count;
    private int _sellCount;
    private InventoryCell _cell;
    [Inject] private DataManager _data;
    private void Start()
    {
        _slider.onValueChanged.AddListener(ChangeCount);
        TradeButton.onClick.AddListener(Trade);
    }
    public void SetItem(InventoryCell cell)
    {
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
    }
    private void SetUI()
    {
        _slider.value = _sellCount;
        _priceText.text = (_sellCount * _price).ToString();
        _sellCountText.text = _sellCount.ToString();
        _curCountText.text = (_count - _sellCount).ToString();
    }
}

