using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class TradePanel : MonoBehaviour
{
    [SerializeField] private Text _curCountText;
    [SerializeField] private Text _sellCountText;
    [SerializeField] private Text _priceText;
    [SerializeField] private Slider _slider;

    private int _price => _cell.Item.Price;
    private int _count;
    private int _sellCount;
    private InventoryCell _cell;
    [Inject] private DataManager _data;
    [Inject] private DialogManager _dialog;
    [Inject] private Sounds _sounds;
    [Inject] private GameModeManager _gameModeManager;

    private void Start()
    {
        _slider.onValueChanged.AddListener(ChangeCount);

        // BUGFIX (round 101): the trade panel visibility was previously
        // driven entirely by GameModeManager.OnTrade UnityEvent's persistent
        // listeners. After scene reload (Continue -> GameScene) the
        // persistent listener can resolve to a destroyed GameObject, in
        // which case Unity silently swallows the NRE and the panel never
        // opens. Subscribe to onChangeMode here as a robust fallback
        // (this is in addition to whatever Inspector listeners exist;
        // both fire - the duplicate-show guard is harmless).
        if (_gameModeManager != null)
        {
            _gameModeManager.onChangeMode += mode =>
            {
                if (mode == GameMode.trade && gameObject != null && !gameObject.activeSelf)
                    gameObject.SetActive(true);
                else if (mode != GameMode.trade && mode != GameMode.inventory
                         && gameObject != null && gameObject.activeSelf)
                    gameObject.SetActive(false);
            };
        }
    }

    public void SetItem(InventoryCell cell)
    {
        gameObject.SetActive(true);
        if (!cell.Item)
        {
            _slider.gameObject.SetActive(false);
            return;
        }
        _slider.gameObject.SetActive(cell.Count > 1);
        _cell = cell;
        _count = cell.Count;
        _sellCount = _count;
        _slider.minValue = 1;
        _slider.maxValue = _count;
        SetUI();
    }

    private void ChangeCount(float value)
    {
        _sellCount = (int)value;
        SetUI();
    }

    public void Trade()
    {
        _data.ChangeMoney(_sellCount * _price);
        _cell.RemoveItem(_sellCount);
        _sounds.UIPlay(EnumData.UISound.sell);
        SetItem(_cell);
    }

    private void SetUI()
    {
        _slider.value = _sellCount;
        _priceText.text = (_sellCount * _price).ToString();
        _sellCountText.text = _sellCount.ToString();
        _curCountText.text = (_count - _sellCount).ToString();
    }

    private void OnDisable()
    {
        // Показываем rohulHelp только при выходе ИЗ режима торговли
        // (не при переходе в диалог и не при закрытии инвентаря)
        if (!_gameModeManager.IsTransitioningToDialog && _gameModeManager.PreviousMode == GameMode.trade)
        {
            _dialog.Remarks.StartRemark(EnumData.RemarksType.rohulHelp);
        }
    }
}