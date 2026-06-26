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

    // BUGFIX (round 17): how many bags the player has already bought across
    // all sessions. Persisted via PlayerPrefs because the trader sells bags
    // in strict order — you can't see bag #2 until you buy bag #1, and the
    // game needs to remember that between launches.
    private const string BagsPurchasedKey = "BagsPurchased";
    private int _bagsPurchased;
    // Buy objects whose ItemPrefab is a BagItem, in the order they were
    // added to MarketManager._items. Index 0 is the first bag, etc.
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

        for (int i = 0; i < _items.Length; i++)
        {
            AddItem(_items[i].item, _items[i].isSingle);
        }

        // After all items are spawned, hide the bags that the player hasn't
        // unlocked yet. Initially only bag[0] is visible; after the player
        // buys it, bag[1] becomes visible, etc.
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

        // BUGFIX (round 17): register bag purchases so we can unlock the
        // next one in sequence. Non-bag items are unaffected.
        if (item.ItemPrefab is BagItem)
        {
            obj.OnBagPurchased += HandleBagPurchased;
            _bagBuyObjects.Add(obj);
        }
    }

    private void HandleBagPurchased()
    {
        _bagsPurchased++;
        PlayerPrefs.SetInt(BagsPurchasedKey, _bagsPurchased);
        PlayerPrefs.Save();
        RefreshBagVisibility();
    }

    // Show only the first _bagsPurchased+1 bags (1, 2, 3...) and hide the
    // rest. Once unlocked, a bag stays unlocked across sessions.
    private void RefreshBagVisibility()
    {
        for (int i = 0; i < _bagBuyObjects.Count; i++)
        {
            if (_bagBuyObjects[i] != null)
                _bagBuyObjects[i].gameObject.SetActive(i <= _bagsPurchased);
        }
    }
}