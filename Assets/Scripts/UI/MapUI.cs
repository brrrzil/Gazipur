using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MapUI : MonoBehaviour
{
    public static MapUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Parent RectTransform that moves (anti-player) and rotates (player yaw) every frame. The MapBackground sprite and the MarkersParent should be children of this transform so the whole map shifts under the player. The MapBackground sprite should be centred (anchoredPosition = 0,0) inside this transform and its size should match the full map in pixels (see _mapPixelSize).")]
    [SerializeField] private RectTransform _mapContent;
    [Tooltip("Optional. A parent RectTransform that has a circular Image + Mask component on it. The _mapContent should be a child of this transform so the rectangular map sprite and the marker icons get clipped to the round shape. If left empty, the map is rendered as a plain rectangle (no clipping).")]
    [SerializeField] private RectTransform _mapMask;
    [Tooltip("Player arrow icon. Should be a child of _mapMask (not of _mapContent) and sit at the centre. It does NOT rotate with the map - the player arrow always points up on the screen because the WORLD rotates under the player.")]
    [SerializeField] private RectTransform _playerArrow;
    [Tooltip("Parent for spawned marker icons. Should be a child of _mapContent (so markers rotate and pan with the map).")]
    [SerializeField] private RectTransform _markersParent;
    [Tooltip("Optional. Parent for marker arrows (shown at the edge of the map when a marker is outside _mapWorldSize / 2). Should be a child of _mapContent so the arrows rotate with the map. Leave empty if you do not want GTA-style edge arrows.")]
    [SerializeField] private RectTransform _markersEdgeParent;
    [Tooltip("Prefab for a single marker icon. The component is just a RectTransform + Image.")]
    [SerializeField] private RectTransform _markerIconPrefab;
    [Tooltip("Prefab for an edge-pointing arrow shown at the rim of the map when the marker is outside the visible area. Same shape as the player arrow or a chevron. Leave empty if edge arrows are not wanted.")]
    [SerializeField] private RectTransform _markerArrowPrefab;

    [Header("World <-> Pixel mapping")]
    [Tooltip("Full size of the location in world units (X, Z). For a 200x200 m location, set to (200, 200). This is the size that fits inside the MapBackground sprite.")]
    [SerializeField] private Vector2 _mapWorldSize = new Vector2(200f, 200f);
    [Tooltip("Size of the MapBackground sprite in pixels. The whole _mapWorldSize fits inside this pixel size. For a 200x200 m location with a 2000x2000 px sprite, the ratio is 10 px / m.")]
    [SerializeField] private float _mapPixelSize = 2000f;
    [Tooltip("World position of the centre of the location. The player and all markers are measured relative to this point. Set this to the (X, _, Z) of the middle of your map - eg (100, 0, 100) for a 200x200 m location whose corner is at the origin.")]
    [SerializeField] private Vector3 _mapCenter = Vector3.zero;

    [Header("Edge arrows")]
    [Tooltip("Pixel radius at which an edge-pointing arrow is shown for an important marker outside the visible map. Auto-computed from the _mapMask RectTransform (half of its size) at OnEnable.")]
    [SerializeField] private float _mapPixelRadius = 150f;

    [Header("Persistence")]
    [Tooltip("PlayerPrefs key prefix for 'marker collected' state. The full key is _collectedPrefix + marker.Id.")]
    [SerializeField] private string _collectedPrefix = "map_marker_collected_";

    private readonly List<Graphic> _graphics = new List<Graphic>();
    private readonly List<Behaviour> _behavioursToToggle = new List<Behaviour>();
    private readonly List<TrackedMarker> _markers = new List<TrackedMarker>();
    private Transform _playerTransform;
    private bool _isOpen;
    private float _pxPerMeter;
    private bool _playerReady;

    [Inject] private PlayerMovement _movement;

    private class TrackedMarker
    {
        public MapMarker Marker;
        public RectTransform Icon;
        public RectTransform Arrow;
    }

    private void Awake()
    {
        Instance = this;
        CollectGraphics();
        SetOpen(false);
    }

    private void OnEnable()
    {
        if (_mapMask != null && _mapMask.sizeDelta.x > 0f)
            _mapPixelRadius = _mapMask.sizeDelta.x * 0.5f;
        _pxPerMeter = _mapPixelSize / Mathf.Max(_mapWorldSize.x, _mapWorldSize.y);
        RebuildMarkers();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Cache every Graphic (Image / Text / RawImage) under the host so we
    // can toggle visibility by enabling / disabling them. We do not use
    // a CanvasGroup because the host (eg PlayerUI) usually has its own
    // CanvasGroup and touching it would hide the whole HUD.
    private void CollectGraphics()
    {
        _graphics.Clear();
        _behavioursToToggle.Clear();
        GetComponentsInChildren(true, _graphics);
        // Also toggle Mask components so the mask does not draw when
        // the map is hidden (saves a draw call per frame).
        var masks = GetComponentsInChildren<Mask>(true);
        _behavioursToToggle.AddRange(masks);
        var rectMasks = GetComponentsInChildren<RectMask2D>(true);
        _behavioursToToggle.AddRange(rectMasks);
    }

    public void SetOpen(bool open)
    {
        _isOpen = open;
        for (int i = 0; i < _graphics.Count; i++)
        {
            if (_graphics[i] != null) _graphics[i].enabled = open;
        }
        for (int i = 0; i < _behavioursToToggle.Count; i++)
        {
            if (_behavioursToToggle[i] != null) _behavioursToToggle[i].enabled = open;
        }
    }

    public void Toggle()
    {
        SetOpen(!_isOpen);
    }

    public void Unlock()
    {
        SetOpen(true);
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

        // Lazy init: Zenject injects AFTER Awake, so the first Update
        // call is the earliest place we can grab the player reference.
        if (!_playerReady)
        {
            if (_movement == null) return;
            _playerTransform = _movement.transform;
            _playerReady = true;
        }
        if (_playerTransform == null || _mapContent == null) return;

        Vector3 playerDelta = _playerTransform.position - _mapCenter;

        _mapContent.localPosition = new Vector3(
            -playerDelta.x * _pxPerMeter,
            -playerDelta.z * _pxPerMeter,
            0f);

        float playerYaw = _playerTransform.eulerAngles.y;
        _mapContent.localRotation = Quaternion.Euler(0f, 0f, playerYaw);

        float cos = Mathf.Cos(-playerYaw * Mathf.Deg2Rad);
        float sin = Mathf.Sin(-playerYaw * Mathf.Deg2Rad);
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            Vector3 d = t.Marker.WorldTransform.position - _mapCenter - playerDelta;
            float rx = d.x * cos - d.z * sin;
            float rz = d.x * sin + d.z * cos;
            float distancePixels = Mathf.Sqrt(rx * rx + rz * rz) * _pxPerMeter;

            if (distancePixels <= _mapPixelRadius)
            {
                if (t.Icon != null)
                {
                    t.Icon.gameObject.SetActive(true);
                    t.Icon.anchoredPosition = new Vector2(rx * _pxPerMeter, rz * _pxPerMeter);
                }
                if (t.Arrow != null) t.Arrow.gameObject.SetActive(false);
            }
            else
            {
                if (t.Icon != null) t.Icon.gameObject.SetActive(false);
                if (t.Arrow != null)
                {
                    t.Arrow.gameObject.SetActive(true);
                    float angle = Mathf.Atan2(rz, rx) * Mathf.Rad2Deg;
                    t.Arrow.anchoredPosition = new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * _mapPixelRadius,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * _mapPixelRadius);
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
