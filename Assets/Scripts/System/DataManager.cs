using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static EnumData;
public class DataManager: MonoBehaviour
{
    [SerializeField] private Text _moneyCount;
    [SerializeField] private Text _moneyToInventoryText;
    [SerializeField] private int _startMoney;

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
        public int index =-1;
        public int count = 0;
    }
    private void Start()
    {
        ChangeMoney(_startMoney);
    }
    public void UpdateInventoryCell(int cellIndex, int itemIndex ,int count)
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
        _moneyCount.text = Money.ToString();
        _moneyToInventoryText.text = Money.ToString();
    }
    public void SetDeffoultHeroState()
    {
        Hero = new HeroInfo() { health = 100, hunger = 50, thirst = 50 };
    }
}
