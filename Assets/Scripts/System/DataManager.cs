using System.Collections.Generic;
using UnityEngine;
using static EnumData;
public class DataManager: MonoBehaviour
{
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
    }
    public void SetDeffoultHeroState()
    {
        Hero = new HeroInfo() { health = 100, hunger = 100, thirst = 100 };
    }
}
