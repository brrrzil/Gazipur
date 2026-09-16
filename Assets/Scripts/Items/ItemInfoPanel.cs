using Assets.SimpleLocalization.Scripts;
using UnityEngine;
using UnityEngine.UI;
using Zenject;


public class ItemInfoPanel : MonoBehaviour
{
    [SerializeField] private LocalizedText _name;
    [SerializeField] private LocalizedText _description;
    [SerializeField] private Image _icon;
    [SerializeField] private Text _priceText;
    [SerializeField] private Text _weightText;
    [SerializeField] private Button _useButton;
    [SerializeField] private Button _dropButton;
    [SerializeField] private Button _sellButton;
    [SerializeField] private TradePanel _tradePanel;
    [SerializeField] private DropItemPanel _dropPanel;

    private InventoryCell _currentCell;
    [Inject] private Inventory _inventory;
    [Inject] private DataManager _data;

    private void Start()
    {
        _useButton.onClick.AddListener(Use);
        _dropButton.onClick.AddListener(Drop);
        _sellButton.onClick.AddListener(Sell);
    }
    public void SetItem(InventoryCell cell, bool isSell)
    {
        var item = cell.Item;
        _currentCell = cell;

        _useButton.gameObject.SetActive(!isSell);
        _dropButton.gameObject.SetActive(!isSell);
        _sellButton.gameObject.SetActive(isSell && cell.Item!=null && !cell.Item.isStatic);

        if (!item)
        {
            _name.Text = "";
            _description.Text = "";
            _priceText.text = "";
            _weightText.text = "";
            _useButton.interactable = false;
            _dropButton.interactable = false;
            _sellButton.interactable = false;
            _icon.enabled = false;
            return;
        }

        _useButton.interactable = item.ItemPrefab is IUsebleItem;
        _dropButton.interactable = !item.isStatic;
        _sellButton.interactable = true;

        SetInfo(item);

        if (isSell)
        {
            _tradePanel.SetItem(cell);
        }
        else
        {
            _tradePanel.gameObject.SetActive(false);
        }
    } 
    public void SetPurchasableItem(ItemData item)
    {
        _tradePanel.gameObject.SetActive(false);
        _useButton.gameObject.SetActive(false);
        _dropButton.gameObject.SetActive(false);
        _sellButton.gameObject.SetActive(false);
        SetInfo(item);
    }
    private void SetInfo(ItemData item)
    {
        _name.Text = item.Name;
        _description.Text = item.Description;
        _priceText.text = item.Price.ToString();
        // BUGFIX (round 21): use '0.#' so whole numbers display as '3'
        // instead of '3.0'. Fractional values like 0.3 still show as '0.3'.
        _weightText.text = item.Weight.ToString("0.#");
        _icon.enabled = true;
        _icon.sprite = item.Icon;
    }
    private void Use()
    {
        _inventory.UseItem(_currentCell);
    }
    private void Drop()
    {
        _dropPanel.SetItem(_currentCell);
    }
    private void Sell()
    {
        _tradePanel.Trade();
        SetItem(_currentCell, true);
    }

}
