using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "ItemData")]
public class ItemData : ScriptableObject
{
    [field: SerializeField] public int Index { get; private set; }
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField, TextArea] public string Description { get; private set; }
    [field: SerializeField, ShowAssetPreview] public Sprite Icon { get; private set; }
    [field: SerializeField] public int Price { get; private set; }
    [field: SerializeField] public int MaxInInventoryCell { get; private set; }
    [field: SerializeField] public float Weight { get; private set; }
    [field: SerializeField] public GameObject ItemPrefab { get; private set; }
}
