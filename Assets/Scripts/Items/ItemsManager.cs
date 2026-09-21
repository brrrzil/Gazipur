using UnityEngine;
using Zenject;

public class ItemsManager : MonoBehaviour
{
    public static ItemsManager Instance { get; private set; }

    [Inject] private DiContainer _container;

    // Cached lookup table built from Resources/Items so GetByIndex()
    // is O(1) on the load path instead of hitting Resources every time.
    private ItemData[] _byIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildIndex();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildIndex()
    {
        var all = Resources.LoadAll<ItemData>("Items");
        // _byIndex is sized to the highest Index value seen. Any out-of-range
        // request returns null (and the caller treats that as "skip this entry").
        int max = -1;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].Index > max) max = all[i].Index;
        if (max < 0) { _byIndex = System.Array.Empty<ItemData>(); return; }
        _byIndex = new ItemData[max + 1];
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Index >= 0 && all[i].Index < _byIndex.Length)
                _byIndex[all[i].Index] = all[i];
        }
    }

    /// <summary>(SaveSystem) Look up an ItemData by its authored Index value.
    /// Returns null if the index is out of range or no asset with that
    /// Index exists.</summary>
    public ItemData GetByIndex(int index)
    {
        if (_byIndex == null) BuildIndex();
        if (index < 0 || index >= _byIndex.Length) return null;
        return _byIndex[index];
    }

    public void DropItem(ItemData item, int count, Vector3 position)
    {
        var obj = _container.InstantiatePrefabForComponent<ItemObject>(item.ItemPrefab);
        obj.transform.position = position;
        obj.SetData(item, count);
    }
}
