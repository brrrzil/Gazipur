using UnityEngine;
using Zenject;

[RequireComponent(typeof(MeshFilter))]
public class ItemObject : InteractObject
{
    [field: SerializeField] public ItemData Item { get; private set; }
    [field: SerializeField] public int Count { get; private set; }
    [Tooltip("Unique id for save persistence (round 101). Empty = not persisted. " +
             "Set this in the Inspector for any item lying on the ground whose " +
             "pickup should survive a Continue (skimmer parts, etc.).")]
    [SerializeField] private string _saveId = "";
    [Inject] private Inventory _inventory;

    private void Start()
    {
        // Despawn immediately if already collected in a previous run.
        if (!string.IsNullOrEmpty(_saveId) && LootPersistence.IsCollected(_saveId))
        {
            Destroy(gameObject);
            return;
        }
        if (Item)
        {
            SetData(Item, Count);
        }
    }

    public void SetData(ItemData item, int count)
    {
        Count = count;
        Item = item;
    }

    public override void Intearct(bool isDown)
    {
        if (!isDown) return;
        int cnt = _inventory.AddItem(Item, Count);
        if (cnt == 0)
        {
            if (!string.IsNullOrEmpty(_saveId))
                LootPersistence.MarkCollected(_saveId);
            Destroy(gameObject);
        }
        else
        {
            Count = cnt;
        }
    }
}