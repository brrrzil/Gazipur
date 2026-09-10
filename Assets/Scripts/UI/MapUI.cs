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
    [Tooltip("Player arrow icon. Should be a child of _mapImage and sit at the centre (anchoredPosition = 0,0). It rotates with _mapImage so the arrow always points up on the screen.")]
    [SerializeField] private RectTransform _playerArrow;
    [Tooltip("Parent for spawned marker icons. Should be a child of _mapImage so markers rotate with the map.")]
    [SerializeField] private RectTransform _markersParent;
    [Tooltip("Prefab for a single marker icon. The component is just a RectTransform + Image - the icon sprite is set at runtime from the MapMarker.Icon field.")]
    [SerializeField] private RectTransform _markerIconPrefab;
    [Tooltip("Half-width of the visible map area in world units. A marker 50 m from the player is drawn at half the radius of the map circle.")]
    [SerializeField] private float _worldRadius = 50f;
    [Tooltip("Pixel radius of the map circle on screen.")]
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

    private struct TrackedMarker
    {
        public MapMarker Marker;
        public RectTransform Icon;
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

    // Called by MapItem.Use after purchase - the map stays open for the
    // rest of the session (consumable gives permanent access to the map UI).
    public void Unlock()
    {
        SetOpen(true);
    }

    private void OnEnable()
    {
        // Refresh markers each time the map is shown - markers may have been
        // added or removed from the scene (eg spawned loot picked up).
        RebuildMarkers();
    }

    private void RebuildMarkers()
    {
        // Clear existing icon transforms.
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            if (_markers[i].Icon != null) Destroy(_markers[i].Icon.gameObject);
        }
        _markers.Clear();

        if (_markerIconPrefab == null) return;

        // Find all MapMarker components in the scene. We do this on enable
        // and not on Update because marker discovery is expensive and the
        // marker set changes only on level transitions / scene reloads.
        var all = FindObjectsByType<MapMarker>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m == null || m.Icon == null) continue;
            if (IsCollected(m.Id)) continue;
            var icon = Instantiate(_markerIconPrefab, _markersParent);
            icon.gameObject.SetActive(true);
            icon.GetComponent<Image>().sprite = m.Icon;
            _markers.Add(new TrackedMarker { Marker = m, Icon = icon });
        }
    }

    private void Update()
    {
        if (!_isOpen) return;
        if (_playerTransform == null) return;

        // Rotate the map so that the player's forward direction is always at
        // the top of the screen. The player arrow is a child of _mapImage,
        // so it rotates with the map and stays pointing up on the screen.
        float playerYaw = _playerTransform.eulerAngles.y;
        if (_mapImage != null)
            _mapImage.localRotation = Quaternion.Euler(0f, 0f, playerYaw);

        // Position each marker icon relative to the map centre. The map is
        // rotated, so a world position to the player's right shows on the
        // map at the world-relative right (after the rotation is applied,
        // the marker still appears at the correct compass direction on
        // the screen because the rotation is applied to the icon too).
        float cos = Mathf.Cos(-playerYaw * Mathf.Deg2Rad);
        float sin = Mathf.Sin(-playerYaw * Mathf.Deg2Rad);
        Vector3 playerPos = _playerTransform.position;
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null || t.Icon == null) continue;
            Vector3 d = t.Marker.WorldTransform.position - playerPos;
            // Rotate the delta so it lines up with the rotated map.
            float rx = d.x * cos - d.z * sin;
            float rz = d.x * sin + d.z * cos;
            // Map world-radius metres to mapPixelRadius pixels.
            float nx = Mathf.Clamp(rx / _worldRadius, -1f, 1f);
            float ny = Mathf.Clamp(rz / _worldRadius, -1f, 1f);
            t.Icon.anchoredPosition = new Vector2(nx * _mapPixelRadius, ny * _mapPixelRadius);
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
