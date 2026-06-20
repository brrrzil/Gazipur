using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "ItemData")]
public class ItemData : ScriptableObject
{
    public enum ItemType
    {
        Generic,
        CapacityUpgrade,
    }

    [field: SerializeField] public int Index { get; private set; }
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField, TextArea] public string Description { get; private set; }
    [field: SerializeField, ShowAssetPreview] public Sprite Icon { get; private set; }
    [field: SerializeField] public int Price { get; private set; }
    [field: SerializeField] public int MaxInInventoryCell { get; private set; }
    [field: SerializeField] public float Weight { get; private set; }
    [field: SerializeField] public GameObject ItemPrefab { get; private set; }

    [field: Header("Upgrade")]
    [field: SerializeField] public ItemType Type { get; private set; } = ItemType.Generic;

    /// <summary>
    /// На сколько увеличивается Capacity инвентаря при подборе/покупке
    /// предмета с Type = CapacityUpgrade. Игнорируется для Generic.
    /// </summary>
    [field: SerializeField, ShowIf(nameof(Type), ItemType.CapacityUpgrade)]
    public float CapacityBonus { get; private set; } = 0f;
}
