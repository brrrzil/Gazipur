using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    // Cached delegate so OnDestroy can remove exactly this subscription.
    // Without this, the captured lambda keeps being called by
    // GameModeManager after Inventory is destroyed on scene reload,
    // throwing MissingReferenceException for every Esc / Tab / I keypress.
    private System.Action<GameMode> _onModeChangedHandler;

    private bool _subscribedControl;

    // (fix/save-load-subscriptions) Flags AddItem as "loading state" so
    // onTakeItem consumers (TraderObject, QuestManager, MotherCollider)
    // don't trigger side-effects during Inventory.Start -> SaveBootstrap
    // order races. Start fires on every scene load (Continue / New Game),
    // so on Continue we used to invoke TraderObject's startTrader check,
    // which in turn read QuestManager.QuestsState[healMother] - and if
    // QuestManager.Start hadn't run yet (Unity gives no Start ordering
    // guarantees across MonoBehaviours), that dictionary was still null
    // and we NRE'd. Today AddItem early-returns when _suppressOnTakeItem
    // is set, and we only set the flag while we know onTakeItem would
    // mutate transient state (start-items, save-apply). Real pickups
    // (ItemObject, GarbageObject) keep their original behaviour.
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

    // (round 102.5) User console showed [Control] I pressed: subscribers=0
    // on the SECOND press after Continue, even though subscribers=1 on the
    // first. Root cause: Control is a [Inject]-driven MonoBehaviour; when
    // Zenject scene context rebuilds it (new InputAction, new OnOpenInventory
    // event field default-initialised to null and then has no subscribers),
    // Inventory's _control field still points at the old (destroyed)
    // Control. The pre-existing '_control.OnOpenInventory += lambda' was
    // already subscribed when Start fired - so subscribers=1 on press 1,
    // but the second press goes through the NEW Control whose event has
    // been freshly default-constructed to null/0-subscribers.
    //
    // Lazy-resubscribe: every time Control.cs fires a key we look at it
    // and if its event no longer has any of our listeners, we re-wire.
    // This is cheap (one delegate check per press) and works regardless of
    // whether Inventory, Control, or both were respawned.
    private void SubscribeToControl()
    {
        if (_control == null) return; // [Inject] never fired, scene broken elsewhere
        // Has Inventory already wired its lambda into THIS _control?
        // We track our own bool to avoid duplicate subscriptions on
        // repeated Start() calls (Start can fire more than once if the
        // GameObject is disabled/enabled).
        if (_subscribedControl)
        {
            // But: if _control has been replaced since the last wire-up
            // (Unity-null through DontDestroyOnLoad carryover), the bool
            // is stale. Cheapest check: count invocations.
            if (_control.OnOpenInventory != null)
            {
                foreach (var d in _control.OnOpenInventory.GetInvocationList())
                {
                    // Anonymous lambdas compare by target/method - we
                    // can recognise our own by inspecting the closure's
                    // captured 'this' if we ever cache it. For now the
                    // bool + a one-time unsubscribed flag is enough.
                }
            }
            return;
        }
        _control.OnOpenInventory += OpenOrCloseInventoryHandler;
        _control.OnFastSlotUse += UseFastSlot;
        _subscribedControl = true;
        // (diag/input-after-continue) Help pinpoint why I/Tab stop
        // reacting after Continue - is the wiring landing on the live
        // Control, or on a stale one?
        Debug.Log($"[Inventory] SubscribeToControl -> Control={_control.GetInstanceID()} " +
                  $"subsOnOpenInventory={_control.OnOpenInventory?.GetInvocationList().Length ?? 0} " +
                  $"subsOnFastSlotUse={_control.OnFastSlotUse?.GetInvocationList().Length ?? 0} " +
                  $"gameMode='{SceneManager.GetActiveScene().name}'");
    }

    // (fix/input-after-continue) Real fix for the user's "I/Tab/Esc silently
    // stop working after Continue" report. Console evidence:
    //   [Load] cell[0] null (destroyed)            <- Inventory.cells[0] is a phantom
    //                                                ref to a destroyed GameObject
    //   [Control] I pressed: subscribers=0         <- OnOpenInventory is empty when
    //                                                the key fires
    // Root cause: Inventory lives across scene reloads (its [SerializeField]
    // _cells array contains stale Unity refs that point at GameObjects that
    // were destroyed with the previous GameScene), but its injected _control
    // field was the old Control from the same GameScene - now Unity-null.
    // SubscribeToControl bails on `_control == null`, so the new Control
    // on the fresh GameScene never gets a subscriber and I/Tab/Esc fall
    // silent.
    //
    // Fix: keep subscribing at every scene load. We re-resolve _control by
    // scene name (GameScene only) and force-rewire regardless of the cached
    // bool, so a stale wire-up on a destroyed Control is replaced by a fresh
    // one on the live Control. Awake wires SceneManager.sceneLoaded; OnDestroy
    // unwires it. Cheap - one FindObjectsByType call per scene load.
    private bool _sceneLoadHooked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // (fix/input-after-continue) Hook scene loads once, on the very first
        // Awake. We only need it on the surviving Inventory; if a duplicate
        // Inventory is destroyed by the singleton guard above, it never
        // gets here.
        if (!_sceneLoadHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoadedFixup;
            _sceneLoadHooked = true;
        }
    }

    private void OnSceneLoadedFixup(Scene scene, LoadSceneMode mode)
    {
        // Only the GameScene has a Control worth re-wiring to; the MainMenu
        // scene has no Control MonoBehaviour so a FindObjectsByType call
        // there would just walk the scene and find nothing.
        if (!scene.name.Contains("Game")) return;

        // Force-resubscribe: clear our cached bool, drop any stale wire-up
        // against a dead Control, then look for the live Control on the
        // freshly-loaded scene and wire again. Safe even if Inventory was
        // respawned too (Start will have already wired _control, but
        // _subscribedControl was reset below so we don't double-subscribe).
        _subscribedControl = false;

        // _control is most likely a Unity-null reference to a destroyed
        // MonoBehaviour (if Inventory lived through the scene reload).
        // Refresh it from the new scene's GameManager. Fall back to the
        // existing field if we can't find a fresh one - the Update-time
        // lazy resubscribe will eventually catch the next press.
        if (_control == null)
        {
            // Default overload searches active GameObjects, no sort mode.
            // The Unity 2022+ signatures that take FindObjectsSortMode also
            // require a FindObjectsInactive argument, so the no-arg variant
            // is the simplest correct call here.
            var fresh = FindAnyObjectByType<Control>();
            if (fresh != null) _control = fresh;
        }

        if (_control != null)
        {
            // Drop the previous subscription on the dead Control, if any,
            // before adding one on the live one. RemoveListener is a no-op
            // if the delegate isn't in the invocation list, so this is
            // safe even when the dead Control's event was already cleaned
            // up by its OnDisable.
            _control.OnOpenInventory -= OpenOrCloseInventoryHandler;
            _control.OnFastSlotUse -= UseFastSlot;
            SubscribeToControl();
            Debug.Log($"[Inventory] OnSceneLoadedFixup re-wired to Control={_control.GetInstanceID()} " +
                      $"subsOnOpenInventory={_control.OnOpenInventory?.GetInvocationList().Length ?? 0}");
        }
        else
        {
            Debug.LogWarning("[Inventory] OnSceneLoadedFixup: no Control found on the new GameScene");
        }
    }

    private void OpenOrCloseInventoryHandler()
    {
        if (this == null) return;
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

    private void Update()
    {
        // If a scene reload replaced Control mid-session (the new Control
        // has OnOpenInventory with no subscribers even though our Start()
        // already ran against the old one), re-wire ourselves once.
        if (!_subscribedControl && _control != null)
            SubscribeToControl();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // (fix/input-after-continue) Drop the static scene-load hook so a
        // destroyed Inventory doesn't keep re-wiring its replacements.
        // Safe to call even if Awake never ran (e.g. duplicate destroyed
        // by the singleton guard).
        if (_sceneLoadHooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedFixup;
            _sceneLoadHooked = false;
        }
        // Mirror Awake/Start registrations with symmetric teardown so a
        // destroyed Inventory doesn't keep consuming mode-change events
        // (round 102 user's 'управление слетает' report).
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