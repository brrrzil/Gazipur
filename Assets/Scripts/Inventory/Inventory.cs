using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public System.Action<ItemData> onTakeItem;
    public HashSet<ToolsType> HaveTools { get; private set; }
    [field: SerializeField] public float Capacity { get; private set; }
    [field: SerializeField] public ItemInfoPanel ItemInfoPanel { get; private set; }
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private FilterBlueprint _filterBlueprint;
    [SerializeField] private Text _weightText;         
    [SerializeField] private Image _weightBar;         
    [SerializeField] private Text _inventoryWeightText;         
    [SerializeField] private Text _cargoPriceText;         
    [SerializeField] private Text _inventoryCargoPriceText;
    [SerializeField] private ItemData[] _startItems;
    [SerializeField] private InventoryCell[] _cells;
    [SerializeField] private FastCell[] _fastCells;
    [SerializeField] private PickedItemUI[] _picedItems;
    [SerializeField] private Image[] _toolsImages;

    /// <summary>(SaveSystem) Read-only access to the cell array so
    /// GamePersistence.LoadIntoGame can write items into cells without
    /// duplicating Inventory's internal bookkeeping.</summary>
    public IReadOnlyList<InventoryCell> Cells => _cells;

    private bool _isOpen;
    private int _picCounter;
    private int _cargoPrice;
    [Inject] DataManager _data;
    [Inject] GameModeManager _gameMode;
    [Inject] GameManager _manager;
    [Inject] DialogManager _dialog;
    [Inject] Control _control;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        HaveTools = new HashSet<ToolsType>();
        _control.OnOpenInventory += () =>
        {
            if(_data.gameMode == GameMode.outdors && !_isOpen)
            {
                _gameMode.ChangeMode(GameMode.inventory);
            }
            else if(_data.gameMode == GameMode.inventory && _isOpen)
            {
                _gameMode.ChangeMode(GameMode.outdors);
            }                 
        };
        _control.OnFastSlotUse += UseFastSlot;
        foreach (var item in _startItems)
        {
            AddItem(item, 1);
        }
        // Seed DataManager's cached inventory snapshot so the first
        // GamePersistence.SaveNow() call (triggered by any producer hook)
        // sees the startItems and not just an empty/null array.
        if (_data != null && _cells != null) _data.UpdateInventory(_cells);

        // BUGFIX (round 101/102): The Inventory panel is wired to GameModeManager.OnInventory
        // UnityEvent. After scene reload (Continue), persistent listeners of that
        // UnityEvent can point at destroyed GameObjects, so the panel fails to
        // open. Subscribe to onChangeMode programmatically as a robust fallback -
        // this also doesn't conflict with inspector listeners, since both fire.
        if (_gameMode != null)
        {
            _gameMode.onChangeMode += mode =>
            {
                if (mode == GameMode.inventory)
                {
                    // ShowPanel itself has a null-check on _inventoryPanel.
                    ShowPanel(true);
                    // Pick the first non-empty cell for the info panel. Without
                    // this the panel opens as an empty box (ItemInfoPanel.SetItem
                    // with a null Item fills fields with empty strings) and the
                    // player thinks the inventory 'didn't open' even though it
                    // did - just blank.
                    if (_cells != null)
                    {
                        InventoryCell firstNonEmpty = null;
                        for (int i = 0; i < _cells.Length; i++)
                            if (_cells[i] != null && _cells[i].Item != null)
                            {
                                firstNonEmpty = _cells[i];
                                break;
                            }
                        if (firstNonEmpty != null)
                            ShowInfoPanel(firstNonEmpty);
                        else if (_cells.Length > 0 && _cells[0] != null)
                            ShowInfoPanel(_cells[0]);
                    }
                }
                else if (mode == GameMode.outdors)
                {
                    ShowPanel(false);
                }
            };
        }
    }

    public int AddItem(ItemData item, int count)
    {
        _picCounter++;
        int startCount = count;
        onTakeItem?.Invoke(item);
        if (CheckTool(item))
        {
            _picedItems[_picCounter % _picedItems.Length].Show(item, count);
            return 0;
        }

        if (CheckFilterBlueprint(item))
        {
            _picedItems[_picCounter % _picedItems.Length].Show(item, count);
            return 0;
        }

        float weight = GetWeight();
        // (round 53) Round cap to 1 decimal too. GetWeight already rounds,
        // but Capacity - weight subtracts two floats and can land on
        // 0.0999999... even when the display reads '0.1 free'. Without
        // this round, the check below (item.Weight * count > cap) trips
        // for a single 0.1-weight item and rejects it, even though the
        // user can clearly see '0.1/0.2' on screen and expects the
        // pickup to fit. Same Mathf.Round pattern as GetWeight (round 21).
        float cap = Mathf.Round((Capacity - weight) * 10f) / 10f;
        int res = 0;
        if (item.Weight * count > cap)
        {
            res = count - (int)(cap / item.Weight);
            count = (int)(cap / item.Weight);
        }
        foreach (var c in _cells)
        {
            if (c.Item == item)
                count = c.AddItem(item, count);

            if (count == 0) break;
        }

        if (count != 0)
        {
            if (item.ItemPrefab is IUsebleItem)
            {
                foreach (var c in _cells)
                {
                    if (c.Item == null)
                        count = c.AddItem(item, count);

                    if (count == 0) break;
                }
            }
            else
            {
                for (int i = _cells.Length - 1; i >= 0; i--)
                {
                    var c = _cells[i];
                    if (c.Item == null)
                        count = c.AddItem(item, count);

                    if (count == 0) break;
                }
            }
        }
        // count — сколько осталось не размещено в ячейках,
        // res — сколько не влезло по весу. Сумма — это то, что осталось
        // в ItemObject (не подобранное игроком). Если 0 — Destroy в
        // ItemObject.Intearct.
        int totalUnpicked = count + res;
        if (totalUnpicked < startCount)
        {
            _picedItems[_picCounter % _picedItems.Length].Show(item, startCount - totalUnpicked);
            // Keep DataManager's cached snapshot in sync so GamePersistence.Collect
            // sees current cell state instead of a stale (or never-initialised)
            // array that would make SaveSystem drop the inventory.
            if (_data != null) _data.UpdateInventory(_cells);
            GamePersistence.SaveNow();
        }
        else
        {
            _dialog.Remarks.StartRemark(RemarksType.inventoryFull);
        }
        return totalUnpicked;
    }

    public float GetWeight()
    {
        float res = 0;
        _cargoPrice = 0;
        foreach (var c in _cells)
        {
            if (c.Item)
            {
                res += c.Item.Weight * c.Count;
                _cargoPrice += c.Item.Price * c.Count;
            }
        }
        // BUGFIX (round 21): weights are guaranteed multiples of 0.1, but
        // float arithmetic (e.g. 0.1f * 29 = 2.9000000953674313) accumulates
        // noise. Without rounding, AddItem sees cap = Capacity - res ≈
        // 0.099999... and rejects a 0.1 item, even though display shows
        // '2.9/3' and the player expects it to fit. Round to 1 decimal so
        // the displayed value and the actual pickup logic match.
        return Mathf.Round(res * 10f) / 10f;
    }

    public void ShowPanel(bool isShow)
    {
        _isOpen = isShow;
        // _inventoryPanel is a [SerializeField] reference to a UI GameObject
        // in the scene. On scene reload (game complete -> restart), the
        // scene is unloaded and the panel GameObject is destroyed, but
        // the Inventory singleton survives (Zenject scene-context service).
        // The GameModeManager.onChangeMode event fires for any subsequent
        // mode change (eg the player presses Inventory after the restart)
        // and routes through this method, which then calls SetActive on
        // the destroyed panel -> MissingReferenceException. The Unity
        // == null guard is the right fix - it returns true for destroyed
        // objects (Unity's operator == override handles the 'fake null'
        // case) and skips the SetActive call.
        if (_inventoryPanel != null) _inventoryPanel.SetActive(isShow);
    }

    public void ChangeCellState(InventoryCell cell)
    {
        // Array.FindIndex returns -1 if `cell` isn't in _cells. The old
        // `if (num < _fastCells.Length)` test passed for num=-1 (since -1
        // is less than any non-negative length) and then indexed
        // _fastCells[-1] - which on a C# array is IndexOutOfRangeException
        // but surfaces as "Object reference not set" via Unity's wrapped
        // exception filtering. Now we bail cleanly if `cell` isn't in
        // _cells, AND we guard each _fastCells[num] access against null.
        int num = System.Array.FindIndex(_cells, i => i == cell);
        if (num < 0) return;
        if (num >= _fastCells.Length)
        {
            // Cell is outside the fast-cell range, only update cargo.
            ChangeCargoValue(Capacity);
            return;
        }
        if (_fastCells[num] != null)
            _fastCells[num].SetItem(cell.Item, cell.Count);
        ChangeCargoValue(Capacity);
    }

    private void UseFastSlot(int number)
    {
        // BUGFIX: Previously a one-liner that indexed _cells[number - 1]
        // without bounds check. If the cell array is shorter than the
        // slot number (e.g. after a corrupted save with inventory
        // re-build), this IndexOutOfRangeException bubbles back into
        // InputSystem as 'callback threw'. Now guard.
        int idx = number - 1;
        if (_cells == null || idx < 0 || idx >= _cells.Length) return;
        if (_cells[idx] == null) return;
        UseItem(_cells[idx]);
    }

    public void UseItem(InventoryCell cell)
    {
        // BUGFIX (round 100.2): The single line 'if (cell.Item == null)'
        // throws NullReferenceException if `cell` itself is a destroyed
        // Unity object (the case after scene reload when Control fires
        // a fast-slot keystroke and the cells the cell array still
        // holds are dead refs). Null-check `cell` explicitly.
        if (cell == null) return;
        if (cell.Item == null) return;

        IUsebleItem item = cell.Item.ItemPrefab as IUsebleItem;
        if (item != null)
        {
            if(item.Use(_manager))
            {
                cell.RemoveItem(1);
                if (_data != null) _data.UpdateInventory(_cells);
                GamePersistence.SaveNow();
            }
        }
    }

    public void ShowInfoPanel(InventoryCell cell)
    {
        ItemInfoPanel.SetItem(cell, _data.gameMode == GameMode.trade);
    }

    public bool CheckTool(ItemData item)
    {
        ToolItem ti = item.ItemPrefab as ToolItem;
        if (ti)
        {
            HaveTools.Add(ti.ToolType);
            _toolsImages[(int)ti.ToolType].sprite = item.Icon;
            IUsebleItem use = ti as IUsebleItem;

            if (use!=null)
                use.Use(_manager);

            return true;
        }
        return false;
    }

    public bool CheckFilterBlueprint(ItemData item)
    {
        FilterPart fp = item.ItemPrefab as FilterPart;
        if (fp)
        {
            _filterBlueprint.AddPart(fp.Part);
            return true;
        }
        return false;
    }

    public void ChangeCargoValue(float value)
    {
        if (value < Capacity) return;
        Capacity = value;
        var wgt = GetWeight();
        // BUGFIX (round 21): use '0.#' instead of 'F1' so whole numbers
        // display as '3' instead of '3.0'. Round 20 used 'F1' which always
        // added a trailing zero; user finds that noisy. '0.#' shows
        // fractional digits only when present.
        _weightText.text = wgt.ToString("0.#") + "/" + Capacity.ToString("0.#");
        _weightBar.fillAmount = wgt / Capacity;
        _inventoryWeightText.text = _weightText.text;
        _cargoPriceText.text = _cargoPrice.ToString();
       _inventoryCargoPriceText.text = _cargoPrice.ToString();
    }

    public InventoryCell CheckMedeicine()
    {
        foreach (var c in _cells)
        {
            if(c.Item!= null && c.Item.ItemPrefab is MedecineItem)
            {
                return c;
            }
        }
        return null;
    }

    public void OnEnable()
    {
        // BUGFIX (M6): the `return` was inside the loop body but outside the
        // `if`, so it always fired after the first iteration — the fallback
        // `ShowInfoPanel(_cells[0])` was unreachable and the panel never
        // updated for the first non-empty cell. Restructure so we find the
        // first non-empty cell and bail, otherwise fall back to cell 0.

        Debug.Log("Inventory.OnEnable() called");

        // Unity-null guard: if no cells are wired in the Inspector
        // (or the references were destroyed during a scene reload),
        // bail before touching _cells[0]. Otherwise OnEnable throws
        // NRE, Start never runs, _control.OnOpenInventory never gets
        // subscribed, and the I key silently does nothing.
        if (_cells == null || _cells.Length == 0) return;

        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] != null && _cells[i].Item)
            {
                ShowInfoPanel(_cells[i]);
                return;
            }
        }
        if (_cells[0] != null) ShowInfoPanel(_cells[0]);
    }
}