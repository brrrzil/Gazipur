using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MarketManager : MonoBehaviour
{
    [field: SerializeField] public float TraderPriceMultiplicator;
    [SerializeField] private BuyItemObject _buyItemPrefab;
    [SerializeField] private Transform _buyItemsPanel;
    [SerializeField] private Item[] _items;
    [Inject] private GameModeManager _modeManager;
    [Inject] private Inventory _inventory;
    [Inject] private DiContainer _container;
    [field: SerializeField] public TradePanel TradePanel;

    // BUGFIX (round 20): how many bags the player has already bought across
    // all sessions. We use this as the index into the sorted bag list to
    // decide which bag to show — only ONE bag is visible at a time.
    private const string BagsPurchasedKey = "BagsPurchased";
    private int _bagsPurchased;
    // Buy objects whose ItemPrefab is a BagItem, sorted cheapest-first.
    private readonly List<BuyItemObject> _bagBuyObjects = new List<BuyItemObject>();

    [System.Serializable]
    public struct Item
    {
        public ItemData item;
        public  bool isSingle;
    }
    private void Start()
    {
        _bagsPurchased = PlayerPrefs.GetInt(BagsPurchasedKey, 0);

        // Pass 1: spawn non-bag items in inspector order.
        foreach (var entry in _items)
        {
            if (entry.item == null) continue;
            if (!(entry.item.ItemPrefab is BagItem))
                AddItem(entry.item, entry.isSingle);
        }

        // Pass 2: collect bags, sort by price (cheapest first). The user's
        // inspector order is ignored — we always show cheap → expensive so
        // the progression makes sense for a new player.
        var bagItems = new List<Item>();
        foreach (var entry in _items)
        {
            if (entry.item != null && entry.item.ItemPrefab is BagItem)
                bagItems.Add(entry);
        }
        bagItems.Sort((a, b) => a.item.Price.CompareTo(b.item.Price));

        // Pass 3: spawn bags in sorted order, all marked isSingle=true so
        // each disappears from the shop after purchase (BuyItemObject
        // handles SetActive(false) on buy when isSingle is true).
        foreach (var entry in bagItems)
            AddItem(entry.item, isSingle: true);

        // Show only the cheapest un-bought bag, hide the rest.
        RefreshBagVisibility();
    }
    public void StartTrade(bool isStart)
    {
        TradePanel.gameObject.SetActive(isStart);
        _inventory.ShowPanel(isStart);
    }
    public void AddItem(ItemData item, bool isSingle)
    {
        var obj = _container.InstantiatePrefabForComponent<BuyItemObject>(_buyItemPrefab, _buyItemsPanel);
        obj.SetItem(item, isSingle);

        if (item.ItemPrefab is BagItem)
        {
            obj.OnBagPurchased += HandleBagPurchased;
            _bagBuyObjects.Add(obj);
        }
    }

    private void HandleBagPurchased()
    {
        // The bag itself is hidden by BuyItemObject.Buy() via isSingle.
        // Just advance the counter so the NEXT bag becomes visible.
        _bagsPurchased++;
        PlayerPrefs.SetInt(BagsPurchasedKey, _bagsPurchased);
        PlayerPrefs.Save();
        RefreshBagVisibility();
    }

    // BUGFIX (round 20): show ONLY the bag at index _bagsPurchased (the
    // cheapest un-bought one). Earlier round 17 used `i <= _bagsPurchased`
    // which made 2 bags visible after the first purchase — the user could
    // skip ahead. Now it's strictly one-at-a-time.
    private void RefreshBagVisibility()
    {
        for (int i = 0; i < _bagBuyObjects.Count; i++)
        {
            if (_bagBuyObjects[i] != null)
                _bagBuyObjects[i].gameObject.SetActive(i == _bagsPurchased);
        }
    }
}
