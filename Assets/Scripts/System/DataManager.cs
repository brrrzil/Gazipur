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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // (input-action-r3) Control is injected by Zenject (scene-scope
    // singleton, same as the rest of GameInstaller). Subscribe on Enable,
    // unsubscribe on Disable - mirrors the rest of the codebase's input
    // wiring and avoids stale subscriptions across scene reloads.
    [Inject] private Control _control;

    private void OnEnable()
    {
        if (_control != null) _control.OnMoneyCheatPressed += OnMoneyCheatPressed;
    }

    private void OnDisable()
    {
        if (_control != null) _control.OnMoneyCheatPressed -= OnMoneyCheatPressed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
}
