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

    // (r5 / ddol-gamescene) Cached delegate for the GameMode change
    // subscription. The handler is a lambda capturing `this`, so we
    // cache the reference here for symmetry with SubscribeToControl -
    // the OnDestroy guard below removes exactly this delegate. With
    // GameScene in DDOL, OnDestroy no longer fires on scene reload
    // (Inventory lives forever), so the cache is mostly diagnostic
    // - we keep it so a future refactor that disables Inventory's
    // GameObject has a clean unsubscribe path.
    private System.Action<GameMode> _onModeChangedHandler;

    // (r5 / ddol-gamescene) Inventory now lives in DDOL with the rest
    // of GameScene, so Start fires once for the session and
    // SubscribeToControl runs once. _subscribedControl is kept as a
    // belt-and-braces against accidental re-Start (e.g. a future
    // SetActive(false)/true), but the scene-reload fixup dance that
    // used to live in OnSceneLoadedFixup is gone.
    private bool _subscribedControl;

    // (fix/save-load-subscriptions) Flags AddItem as "loading state" so
    // onTakeItem consumers (TraderObject, QuestManager, MotherCollider)
    // don't trigger side-effects during Inventory.Start -> SaveBootstrap
    // order races. Start fires once per app session now (DDOL), but the
    // flag is still useful while we apply the saved blob (LoadIntoGame
    // calls AddItem from the save path) so onTakeItem side-effects don't
    // trip on already-loaded items.
    private bool _suppressOnTakeItem;

    private void Start()
    {
        HaveTools = new HashSet<ToolsType>();
        SubscribeToControl();

        // (fix/save-load-subscriptions) Only seed _startItems when there
        // is no save slot. On Continue the SaveBootstrap is about to call
        // LoadIntoGame, which clears every cell and refills from the saved
        // blob - adding start-items first would briefly populate the
        // inventory with the wrong contents (and let the onTakeItem side-
        // effects above run once with the wrong item, twice if a start-
        // item is also in the save).
        bool hasSave = SaveSystem.HasSave();
        if (!hasSave && _startItems != null)
        {
            // Suppress onTakeItem while we seed start-items: QuestManager
            // and TraderObject may not have completed their own Start()
            // yet, and reading their mutable state from inside AddItem is
            // a known race.
            _suppressOnTakeItem = true;
            try
            {
                foreach (var item in _startItems)
                {
                    AddItem(item, 1);
                }
            }
            finally
            {
                _suppressOnTakeItem = false;
            }
        }
        // Seed DataManager's cached inventory snapshot so the first
        // GamePersistence.SaveNow() call (triggered by any producer hook)
        // sees the startItems and not just an empty/null array.
        if (_data != null && _cells != null) _data.UpdateInventory(_cells);

        // Cached delegate so OnDestroy can remove exactly this subscription.
        // Without this, the captured lambda keeps being called by
        // GameModeManager after Inventory is destroyed on scene reload,
        // throwing MissingReferenceException for every Esc / Tab / I keypress.
        if (_gameMode != null)
        {
            _onModeChangedHandler = mode =>
            {
                // Unity-null check on `this` - same reason as TradePanel.
                // The lambda is captured by GameModeManager and outlives
                // Inventory on scene reload.
                if (this == null) return;
                if (mode == GameMode.inventory)
                {
                    ShowPanel(true);
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
            _gameMode.onChangeMode += _onModeChangedHandler;
        }
    }

    // (r5 / ddol-gamescene) Start now fires once per session, not per
    // scene load. The old lazy-resubscribe-in-Update and
    // sceneLoadFixup dance was needed because Inventory lived across
    // scene reloads (DDOL via Sounds.Init hoisting its transform.root)
    // and ended up with stale _control refs - the new Control on the
    // fresh GameScene had subscribers=0. With GameScene in DDOL and
    // Inventory living once, Start fires once, SubscribeToControl runs
    // once, _control is the live one forever. The complexity below
    // was load-bearing for the previous design; here it's a simple
    // 'subscribe, mark subscribed, done'.
    private void SubscribeToControl()
    {
        if (_control == null) return; // [Inject] never fired, scene broken elsewhere
        if (_subscribedControl) return; // already wired (Start ran twice, e.g. SetActive)
        _control.OnOpenInventory += OpenOrCloseInventoryHandler;
        _control.OnFastSlotUse += UseFastSlot;
        _subscribedControl = true;
        Debug.Log($"[Inventory] SubscribeToControl -> Control={_control.GetInstanceID()} " +
                  $"subsOnOpenInventory={_control.OnOpenInventory?.GetInvocationList().Length ?? 0} " +
                  $"subsOnFastSlotUse={_control.OnFastSlotUse?.GetInvocationList().Length ?? 0} " +
                  $"gameMode='{gameObject.scene.name}'");
    }

    private void Awake()
    {
        Debug.Log($"[Inventory#{GetInstanceID()}] Awake Instance={(Instance == null ? "null" : Instance.GetInstanceID().ToString())} _control={(_control == null ? "null" : _control.GetInstanceID().ToString())} scene='{gameObject.scene.name}'");
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[Inventory#{GetInstanceID()}] duplicate detected, destroying self");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OpenOrCloseInventoryHandler()
    {
        if (_data == null || _gameMode == null) return;
        if (_data.gameMode == GameMode.outdors && !_isOpen)
        {
            _gameMode.ChangeMode(GameMode.inventory);
        }
        else if (_data.gameMode == GameMode.inventory && _isOpen)
        {
            _gameMode.ChangeMode(GameMode.outdors);
        }
    }

    private void OnDestroy()
    {
        // (r5) With GameScene in DDOL Inventory only ever dies at app
        // shutdown, so the unsubscribes below are belt-and-braces.
        // They would matter if Inventory's GameObject were disabled
        // and re-enabled (which re-runs Awake/Start and would double-
        // subscribe), but for the current DDOL setup Inventory never
        // re-Spawns.
        if (Instance == this) Instance = null;
        if (_gameMode != null && _onModeChangedHandler != null)
            _gameMode.onChangeMode -= _onModeChangedHandler;
        if (_control != null)
        {
            _control.OnOpenInventory -= OpenOrCloseInventoryHandler;
            _control.OnFastSlotUse -= UseFastSlot;
            _subscribedControl = false;
        }
    }

    public int AddItem(ItemData item, int count)
    {
        _picCounter++;
        int startCount = count;
        // (fix/save-load-subscriptions) Skip the onTakeItem side-effects
        // when we're seeding _startItems in Start() or while the saved
        // blob is being applied by SaveBootstrap. QuestManager /
        // TraderObject / MotherCollider may not have finished their own
        // Start() yet, and invoking their handlers now produces NREs on
        // `_quest.QuestsState[healMother]` (QuestsState is initialised in
        // QuestManager.Start).
        if (!_suppressOnTakeItem) onTakeItem?.Invoke(item);
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

    // (r5 / new-game) Empties every cell and re-seeds the configured
    // _startItems. Called by GameSession.ResetForNewGame when the player
    // hits 'New Game' from the main menu. With GameScene in DDOL the
    // Inventory MonoBehaviour lives once for the session, so without
    // this reset a Continue followed by New Game would keep the
    // player's items.
    public void ResetToDefaults()
    {
        if (_cells != null)
        {
            foreach (var c in _cells)
            {
                if (c != null && c.Item != null) c.RemoveItem(c.Count);
            }
        }
        // Re-seed _startItems using the same guarded path that
        // Inventory.Start uses for the very first load: suppress
        // onTakeItem side-effects while we mutate transient state,
        // then sync DataManager's cached snapshot.
        _suppressOnTakeItem = true;
        try
        {
            if (_startItems != null)
            {
                foreach (var item in _startItems) AddItem(item, 1);
            }
        }
        finally
        {
            _suppressOnTakeItem = false;
        }
        if (_data != null && _cells != null) _data.UpdateInventory(_cells);
        // Re-publish UI weight/cargo counters that the cell mutation
        // doesn't trigger on its own.
        ChangeCargoValue(Capacity);
        HaveTools = new HashSet<ToolsType>();
        Debug.Log("[Inventory] ResetToDefaults done");
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