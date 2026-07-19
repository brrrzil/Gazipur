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
    // (round 62b) DialogManager is now injected here so
    // StartTrade(false) can fire the traderAfterBuy dialog on
    // trade-panel close, not on the earlier AddItem call. See the
    // round 62b note in TraderObject.cs for the full reasoning.
    [Inject] private DialogManager _dialog;
    [field: SerializeField] public TradePanel TradePanel;

    // BUGFIX (round 27): bags are now SESSION-ONLY. Previously we persisted
    // _bagsPurchased to PlayerPrefs which meant the cheapest bag stayed
    // hidden across play sessions — the user had to do Edit → Clear All
    // PlayerPrefs every time they wanted to test the bag sequence. Now
    // every new game session starts with all bags available again. The
    // one-at-a-time show logic still applies within a single session.
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
        // _bagsPurchased starts at 0 every session (session-only, no PlayerPrefs).
        _bagsPurchased = 0;

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
        // (round 62b) On trade-panel close, fire the trader
        // post-purchase line. The previous wiring fired the
        // same dialog from TraderObject on AddItem, which the
        // player experienced as 'right after I close the
        // inventory'. They asked for it to be tied to the
        // trade-panel close instead, so the line plays once
        // when they walk away from the trader. DialogManager
        // is isOneTime so it will not re-fire on subsequent
        // closes even if the player re-opens the panel.
        if (!isStart)
        {
            _dialog.StartDialog(DialogType.traderAfterBuy);
        }
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
        // No PlayerPrefs — bag state resets every game session.
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
