using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [SerializeField] private Text _moneyCount;
    [SerializeField] private Text _moneyToInventoryText;
    [SerializeField] private int _startMoney;
    // (input-action-r3) Amount added per MoneyCheat (P) press. Round 81
    // DebugCheat also had a 1000 default, kept for parity.
    [SerializeField] private int _moneyCheatAmount = 1000;

    public System.Action onChangeMoney;
    public int Money { get; private set; }
    public GameMode gameMode;
    public HeroInfo Hero { get; private set; }
    public ItemInfo[] Inventory { get; private set; }
    public List<ItemInfo> HomeBox { get; private set; }

    [System.Serializable]
    public class HeroInfo
    {
        public float health;
        public float thirst;
        public float hunger;
    }

    [System.Serializable]
    public class ItemInfo
    {
        public int index = -1;
        public int count = 0;
    }

    // (input-action-r3) Control is injected by Zenject (scene-scope
    // singleton, same as the rest of GameInstaller). Declared above
    // Awake so the subscription in Awake reads as straightforward
    // field-access rather than depending on declaration order.
    [Inject] private Control _control;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // (r5 / ddol-gamescene) Subscribe here instead of OnEnable so
        // we wire once for the session. The old OnEnable/OnDisable
        // dance was needed because Control was scene-bound and could
        // be destroyed+re-injected by Zenject; now both live in DDOL
        // and Awake fires once. Subscribe in Awake so we know the
        // [Inject] has resolved (Awake is after the injection point).
        if (_control != null) _control.OnMoneyCheatPressed += OnMoneyCheatPressed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // (r5) Symmetric unsubscribe. With DDOL this only fires at app
        // shutdown, but the unsubscribe is one line and keeps the
        // subscription balance symmetric.
        if (_control != null) _control.OnMoneyCheatPressed -= OnMoneyCheatPressed;
    }

    private void Start()
    {
        ChangeMoney(_startMoney);
    }
    public void UpdateInventoryCell(int cellIndex, int itemIndex, int count)
    {
        Inventory[cellIndex].count = count;
        Inventory[cellIndex].index = itemIndex;
    }
    public void UpdateInventory(InventoryCell[] cells)
    {
        Inventory = new ItemInfo[cells.Length];
        for (int i = 0; i < Inventory.Length; i++)
        {
            if (cells[i].Item != null)
            {
                Inventory[i] = new ItemInfo() { count = cells[i].Count, index = cells[i].Item.Index };
                continue;
            }
            Inventory[i] = new ItemInfo();
        }
    }
    public void ChangeMoney(int count)
    {
        Money += count;
        onChangeMoney?.Invoke();
        if (_moneyCount != null) _moneyCount.text = Money.ToString();
        if (_moneyToInventoryText != null) _moneyToInventoryText.text = Money.ToString();
        GamePersistence.SaveNow();
    }

    // (input-action-r3) P key was previously handled by DebugCheat.cs via
    // Keyboard.current polling. Moved the actual "+money" effect here so
    // all P/M/I/Esc/1-5 share the same Control.cs dispatch path and the
    // same project policy (no legacy Input class). ChangeMoney already
    // calls onChangeMoney and SaveNow, so the UI counter refreshes and
    // the save blob is updated within the same frame.
    private void OnMoneyCheatPressed()
    {
        ChangeMoney(_moneyCheatAmount);
    }
    /// <summary>Set Money to an absolute value (used by SaveSystem.Load).</summary>
    public void SetMoney(int amount)
    {
        Money = amount;
        onChangeMoney?.Invoke();
        if (_moneyCount != null) _moneyCount.text = Money.ToString();
        if (_moneyToInventoryText != null) _moneyToInventoryText.text = Money.ToString();
    }
    public void SetDeffoultHeroState()
    {
        Hero = new HeroInfo() { health = 100, hunger = 50, thirst = 50 };
    }

    // (r5 / new-game) Resets all live state to scene-authored defaults.
    // Called by GameSession.ResetForNewGame when the player hits 'New Game'
    // from the main menu. With GameScene in DDOL, SceneManager.LoadScene
    // is a no-op for the gameplay root, so a Continue followed by New
    // Game would otherwise keep the previous Money/Hero/Inventory
    // because Awake/Start only fire once per app session.
    public void ResetToDefaults()
    {
        // Money: subtract the current value so OnChangeMoney fires with
        // a delta, then add _startMoney. This goes through the normal
        // ChangeMoney path (SaveNow included) so the next SaveNow writes
        // the new value rather than the stale one.
        if (Money != 0) ChangeMoney(-Money);
        ChangeMoney(_startMoney);
        // Hero: full health, mid hunger/thirst.
        SetDeffoultHeroState();
        // Inventory: clear the cached snapshot so the next Collect
        // sees empty cells.
        if (Inventory != null)
        {
            for (int i = 0; i < Inventory.Length; i++)
                Inventory[i] = new ItemInfo();
        }
        HomeBox = null;
        gameMode = GameMode.outdors;
        // Force the UI text to repaint (in case onChangeMoney already
        // updated it to 0 before _startMoney was added).
        if (_moneyCount != null) _moneyCount.text = Money.ToString();
        if (_moneyToInventoryText != null) _moneyToInventoryText.text = Money.ToString();
        Debug.Log("[DataManager] ResetToDefaults done");
    }
}
