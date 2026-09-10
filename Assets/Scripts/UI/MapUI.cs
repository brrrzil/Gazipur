using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[RequireComponent(typeof(CanvasGroup))]
public class MapUI : MonoBehaviour
{
    public static MapUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Round map image. The map rotates inside this RectTransform so the icons (markers) rotate with the world while the player arrow stays fixed at the top.")]
    [SerializeField] private RectTransform _mapImage;
    [Tooltip("Optional. A parent RectTransform that has a circular Image + Mask component on it. The _mapImage and _markersParent should be children of this transform so the rectangular map sprite and the marker icons get clipped to the round shape. If left empty, the map is rendered as a plain rectangle (no clipping).")]
    [SerializeField] private RectTransform _mapMask;
    [Tooltip("Player arrow icon. Should be a child of _mapImage (not of _mapMask) and sit at the centre. It rotates with _mapImage so the arrow always points up on the screen.")]
    [SerializeField] private RectTransform _playerArrow;
    [Tooltip("Parent for spawned marker icons. Should be a child of _mapImage (and therefore a child of _mapMask, so icons get clipped to the round shape).")]
    [SerializeField] private RectTransform _markersParent;
    [Tooltip("Optional. Parent for marker arrows (shown at the edge of the map when a marker is outside _worldRadius). Should be a child of _mapImage so the arrows rotate with the map. Leave empty if you do not want GTA-style edge arrows.")]
    [SerializeField] private RectTransform _markersEdgeParent;
    [Tooltip("Prefab for a single marker icon. The component is just a RectTransform + Image.")]
    [SerializeField] private RectTransform _markerIconPrefab;
    [Tooltip("Prefab for an edge-pointing arrow shown at the rim of the map when the marker is outside _worldRadius. Same shape as the player arrow or a chevron. Leave empty if edge arrows are not wanted.")]
    [SerializeField] private RectTransform _markerArrowPrefab;
    [Tooltip("Half-width of the visible map area in world units. A marker 50 m from the player is drawn at half the radius of the map circle.")]
    [SerializeField] private float _worldRadius = 50f;
    [Tooltip("Pixel radius of the map circle on screen. Markers and arrows are positioned within this radius from the centre.")]
    [SerializeField] private float _mapPixelRadius = 150f;

    [Header("Persistence")]
    [Tooltip("PlayerPrefs key prefix for 'marker collected' state. The full key is _collectedPrefix + marker.Id.")]
    [SerializeField] private string _collectedPrefix = "map_marker_collected_";

    private CanvasGroup _canvasGroup;
    private readonly List<TrackedMarker> _markers = new List<TrackedMarker>();
    private Camera _camera;
    private Transform _playerTransform;
    private bool _isOpen;

    [Inject] private PlayerMovement _movement;

    private class TrackedMarker
    {
        public MapMarker Marker;
        public RectTransform Icon;
        public RectTransform Arrow;  // null if marker is on the map (not at the edge)
    }

    private void Awake()
    {
        Instance = this;
        _canvasGroup = GetComponent<CanvasGroup>();
        _camera = Camera.main;
        if (_movement != null) _playerTransform = _movement.transform;
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetOpen(bool open)
    {
        _isOpen = open;
        _canvasGroup.alpha = open ? 1f : 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    public void Toggle()
    {
        SetOpen(!_isOpen);
    }

    public void Unlock()
    {
        SetOpen(true);
    }

    private void OnEnable()
    {
        RebuildMarkers();
    }

    private void RebuildMarkers()
    {
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            var t = _markers[i];
            if (t.Icon != null) Destroy(t.Icon.gameObject);
            if (t.Arrow != null) Destroy(t.Arrow.gameObject);
        }
        _markers.Clear();

        if (_markerIconPrefab == null) return;

        var all = FindObjectsByType<MapMarker>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m == null || m.Icon == null) continue;
            if (IsCollected(m.Id)) continue;
            var icon = Instantiate(_markerIconPrefab, _markersParent);
            icon.gameObject.SetActive(true);
            icon.GetComponent<Image>().sprite = m.Icon;

            RectTransform arrow = null;
            if (_markerArrowPrefab != null && _markersEdgeParent != null)
            {
                arrow = Instantiate(_markerArrowPrefab, _markersEdgeParent);
                arrow.gameObject.SetActive(true);
            }

            _markers.Add(new TrackedMarker { Marker = m, Icon = icon, Arrow = arrow });
        }
    }

    private void Update()
    {
        if (!_isOpen) return;
        if (_playerTransform == null) return;

        // Rotate the map so the player's forward direction is always at
        // the top of the screen. The player arrow is a child of _mapImage,
        // so it rotates with the map and stays pointing up on the screen.
        float playerYaw = _playerTransform.eulerAngles.y;
        if (_mapImage != null)
            _mapImage.localRotation = Quaternion.Euler(0f, 0f, playerYaw);

        // Inverse-rotate the world delta so the marker lines up with the
        // rotated map. A marker that is physically to the player's right
        // (x > 0) should appear on the map to the right of the arrow -
        // the rotation moves the map under the player, so the marker is
        // also moved.
        float cos = Mathf.Cos(-playerYaw * Mathf.Deg2Rad);
        float sin = Mathf.Sin(-playerYaw * Mathf.Deg2Rad);
        Vector3 playerPos = _playerTransform.position;
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            Vector3 d = t.Marker.WorldTransform.position - playerPos;
            float rx = d.x * cos - d.z * sin;
            float rz = d.x * sin + d.z * cos;
            float distance = Mathf.Sqrt(rx * rx + rz * rz);

            if (distance <= _worldRadius)
            {
                // On the map: show the icon at the proportional position.
                // If a _mapMask is configured, the icon is a child of
                // _mapImage which is a child of _mapMask, so the icon gets
                // clipped to the round shape automatically.
                if (t.Icon != null)
                {
                    float nx = Mathf.Clamp(rx / _worldRadius, -1f, 1f);
                    float ny = Mathf.Clamp(rz / _worldRadius, -1f, 1f);
                    t.Icon.gameObject.SetActive(true);
                    t.Icon.anchoredPosition = new Vector2(nx * _mapPixelRadius, ny * _mapPixelRadius);
                }
                if (t.Arrow != null) t.Arrow.gameObject.SetActive(false);
            }
            else
            {
                // Off the map (GTA-style): clamp the icon to the edge and
                // show an arrow at the rim pointing toward the marker's
                // direction from the player.
                if (t.Icon != null) t.Icon.gameObject.SetActive(false);
                if (t.Arrow != null)
                {
                    t.Arrow.gameObject.SetActive(true);
                    float angle = Mathf.Atan2(rz, rx) * Mathf.Rad2Deg;
                    // Position the arrow at the rim, pointing outward.
                    t.Arrow.anchoredPosition = new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * _mapPixelRadius,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * _mapPixelRadius);
                    // Rotate the arrow so its 'forward' (assumed +X or +Y in
                    // local space) points outward. Adjust the 90 if the
                    // arrow sprite is oriented differently.
                    t.Arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                }
            }
        }
    }

    private bool IsCollected(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return PlayerPrefs.GetInt(_collectedPrefix + id, 0) == 1;
    }

    public void MarkCollected(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        PlayerPrefs.SetInt(_collectedPrefix + id, 1);
        PlayerPrefs.Save();
    }
}
