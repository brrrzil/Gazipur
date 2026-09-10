using UnityEngine;

[DisallowMultipleComponent]
public class MapMarker : MonoBehaviour
{
    [Tooltip("Icon to display on the map. Set in the Inspector by the artist.")]
    [SerializeField] private Sprite _icon;
    [Tooltip("Unique id for this marker. Used to remember 'collected' state - once a marker is collected, the map hides its icon.")]
    [SerializeField] private string _id;

    public Sprite Icon => _icon;
    public string Id => _id;
    public Transform WorldTransform => transform;
}
