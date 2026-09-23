using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using static EnumData;

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

        // BUGFIX (round 102): GameModeManager.OnTrade UnityEvent has no
        // persistent listener wired to MarketManager.StartTrade in the
        // prefab or scene (verified by grep on GameScene.unity and
        // GameManager.prefab - zero `m_MethodName: StartTrade` entries).
        // Without that listener `ChangeMode(GameMode.trade)` opens the
        // mode internally but never actually shows the trade UI - the
        // player presses Esc after a Trader dialog and nothing visible
        // happens. Subscribe programmatically via onChangeMode here as
        // an additive fallback that doesn't conflict with whatever
        // inspector listeners the user wires later.
        if (_modeManager != null)
        {
            _onModeChangedHandler = mode =>
            {
                // (round 102) Unity-null check on captured `this` - the
                // lambda is held by GameModeManager across scene reloads.
                if (this == null) return;
                if (TradePanel == null) return; // also dead on scene reload
                if (mode == GameMode.trade)
                    StartTrade(true);
                else if (mode == GameMode.inventory && TradePanel.gameObject != null
                         && TradePanel.gameObject.activeSelf)
                    StartTrade(false);
            };
            _modeManager.onChangeMode += _onModeChangedHandler;
        }
    }

    private System.Action<GameMode> _onModeChangedHandler;

    private void OnDestroy()
    {
        // (round 102) Mirror Start's `_modeManager.onChangeMode += ...`
        // with symmetric `-=` so a destroyed MarketManager doesn't keep
        // calling StartTrade() every time the mode changes.
        if (_modeManager != null && _onModeChangedHandler != null)
            _modeManager.onChangeMode -= _onModeChangedHandler;
    }

    public void StartTrade(bool isStart)
    {
        Debug.Log($"StartTrade called with isStart={isStart}");
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