using UnityEngine;

[DisallowMultipleComponent]
public class MapMarker : MonoBehaviour
{
    [Tooltip("Icon to display on the map. Set in the Inspector by the artist.")]
    [SerializeField] private Sprite _icon;
    [Tooltip("Unique id for this marker. Used to remember 'collected' state - once a marker is collected, the map hides its icon.")]
    [SerializeField] private string _id;
    [Tooltip("If true, the marker is shown on the map edge (GTA-style) when it is outside the visible map radius, so the player can see the direction to the marker even from far away. Use for important quest / objective markers. Regular loot and trivial markers should leave this off so they do not clutter the rim of the map.")]
    [SerializeField] private bool _isImportant;

    public Sprite Icon => _icon;
    public string Id => _id;
    public bool IsImportant => _isImportant;
    public Transform WorldTransform => transform;
}
