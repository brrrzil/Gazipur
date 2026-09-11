using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Zenject;

public class MapUI : MonoBehaviour
{
    public static MapUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Optional. The root GameObject that wraps the whole minimap hierarchy (MapMask + MapContent + PlayerArrow + MarkersParent). If set, the map is hidden / shown via SetActive on this GameObject. If left empty, the map falls back to toggling Graphic.enabled on each UI element.")]
    [SerializeField] private GameObject _mapRoot;
    [Tooltip("Parent RectTransform that owns the rotation. The script forces its pivot to (0.5, 0.5) and anchoredPosition to (0, 0) at startup so the rotation always pivots around the centre of MapMask regardless of how it is configured in the Inspector. Stays at localPosition (0, 0, 0) - movement is delegated to its child _mapBackground.")]
    [SerializeField] private RectTransform _mapContent;
    [Tooltip("Optional. A parent RectTransform that has a circular Image + Mask component on it. _mapContent should be a child of _mapMask, centred at (0, 0).")]
    [SerializeField] private RectTransform _mapMask;
    [Tooltip("Player arrow icon. Should be a child of _mapMask (not of _mapContent) and sit at the centre. It does NOT rotate with the map.")]
    [SerializeField] private RectTransform _playerArrow;
    [Tooltip("Required. RectTransform of the rectangular map sprite. Its anchoredPosition is updated every frame so the visible part of the location stays centred on the player. Should be a child of _mapContent.")]
    [SerializeField] private RectTransform _mapBackground;
    [Tooltip("Parent for spawned marker icons. Should be a child of _mapContent.")]
    [SerializeField] private RectTransform _markersParent;
    [Tooltip("Optional. Parent for marker arrows. Should be a child of _mapContent. Leave empty if you do not want GTA-style edge arrows.")]
    [SerializeField] private RectTransform _markersEdgeParent;
    [Tooltip("Prefab for a single marker icon.")]
    [SerializeField] private RectTransform _markerIconPrefab;
    [Tooltip("Prefab for an edge-pointing arrow. Leave empty if edge arrows are not wanted.")]
    [SerializeField] private RectTransform _markerArrowPrefab;

    [Header("World <-> Pixel mapping")]
    [Tooltip("Full size of the location in world units (X, Z). For a 200x200 m location, set to (200, 200).")]
    [SerializeField] private Vector2 _mapWorldSize = new Vector2(200f, 200f);
    [Tooltip("Size of the MapBackground sprite in pixels. The whole _mapWorldSize fits inside this pixel size.")]
    [SerializeField] private float _mapPixelSize = 2000f;
    [Tooltip("World position of the centre of the location. The player and all markers are measured relative to this point.")]
    [SerializeField] private Vector3 _mapCenter = Vector3.zero;

    [Header("Edge arrows")]
    [Tooltip("Pixel radius at which an edge-pointing arrow is shown for an important marker outside the visible map. Auto-computed from the _mapMask RectTransform (half of its size) at OnEnable.")]
    [SerializeField] private float _mapPixelRadius = 150f;

    [Header("Rotation")]
    [Tooltip("Extra rotation in degrees added to the player's yaw when the map rotates. Default 0. Set this to compensate for a map sprite that is drawn upside-down (set 180), or to flip the rotation direction (set 180), or to align the sprite's 'north' to the world's '+Z' (typically 0 for a sprite that already has north pointing up).")]
    [SerializeField] private float _mapRotationOffset = 0f;

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
    private bool _hasRoot;
    private bool _markersBuilt;
    private bool _graphicsCollected;

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
        _hasRoot = _mapRoot != null;
        if (!_hasRoot) CollectGraphics();

        // Force _mapContent to a clean rotation pivot: pivot (0.5, 0.5)
        // and anchoredPosition (0, 0) inside _mapMask. Whatever the
        // user has set in the Inspector, the rotation will now pivot
        // around the centre of MapMask.
        if (_mapContent != null && _mapMask != null)
        {
            _mapContent.SetParent(_mapMask, false);
            _mapContent.anchorMin = new Vector2(0.5f, 0.5f);
            _mapContent.anchorMax = new Vector2(0.5f, 0.5f);
            _mapContent.pivot = new Vector2(0.5f, 0.5f);
            _mapContent.anchoredPosition = Vector2.zero;
            _mapContent.sizeDelta = Vector2.zero;
            _mapContent.localRotation = Quaternion.identity;
            _mapContent.localPosition = Vector3.zero;
        }

        SetOpen(false);
    }

    private void OnEnable()
    {
        if (_mapMask != null && _mapMask.sizeDelta.x > 0f)
            _mapPixelRadius = _mapMask.sizeDelta.x * 0.5f;
        _pxPerMeter = _mapPixelSize / Mathf.Max(_mapWorldSize.x, _mapWorldSize.y);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void CollectGraphics()
    {
        _graphics.Clear();
        _behavioursToToggle.Clear();

        if (_mapMask != null)
        {
            var maskImage = _mapMask.GetComponent<Graphic>();
            if (maskImage != null) _graphics.Add(maskImage);
        }

        if (_mapContent != null)
            _mapContent.GetComponentsInChildren(true, _graphics);

        if (_playerArrow != null)
        {
            var arrowGraphics = _playerArrow.GetComponentsInChildren<Graphic>(true);
            _graphics.AddRange(arrowGraphics);
        }

        if (_mapMask != null)
        {
            var masks = _mapMask.GetComponentsInChildren<Mask>(true);
            _behavioursToToggle.AddRange(masks);
            var rectMasks = _mapMask.GetComponentsInChildren<RectMask2D>(true);
            _behavioursToToggle.AddRange(rectMasks);
        }
    }

    private void EnsureGraphicsCollected()
    {
        if (_graphicsCollected) return;
        CollectGraphics();
        _graphicsCollected = true;
    }

    public void SetOpen(bool open)
    {
        _isOpen = open;

        if (_hasRoot)
        {
            _mapRoot.SetActive(open);
            if (open && !_markersBuilt) RebuildMarkers();
            return;
        }

        if (open && !_markersBuilt) RebuildMarkers();

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

        if (_markerIconPrefab == null)
        {
            _markersBuilt = true;
            return;
        }

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
        _markersBuilt = true;
    }

    private void Update()
    {
        if (Keyboard.current != null
            && Keyboard.current.mKey.wasPressedThisFrame)
        {
            Toggle();
        }

        if (!_isOpen) return;

        if (!_playerReady)
        {
            if (_movement == null) return;
            _playerTransform = _movement.transform;
            _playerReady = true;
        }
        if (_playerTransform == null || _mapContent == null) return;

        Vector3 playerDelta = _playerTransform.position - _mapCenter;
        float playerYaw = _playerTransform.eulerAngles.y;

        // Background shift, clamped so the sprite always covers the mask.
        float maxShift = Mathf.Max(0f, _mapPixelSize * 0.5f - _mapPixelRadius);
        float shiftX = Mathf.Clamp(-playerDelta.x * _pxPerMeter, -maxShift, maxShift);
        float shiftY = Mathf.Clamp(-playerDelta.z * _pxPerMeter, -maxShift, maxShift);

        // _mapContent stays at the centre of MapMask (Awake forced the
        // pivot and anchoredPosition). Its rotation pivots around that
        // centre, which is also the centre of the radar where the
        // player arrow sits. Movement is delegated to _mapBackground.
        _mapContent.localPosition = Vector3.zero;
        _mapContent.localRotation = Quaternion.Euler(0f, 0f, playerYaw + _mapRotationOffset);

        if (_mapBackground != null)
            _mapBackground.anchoredPosition = new Vector2(shiftX, shiftY);

        // Markers: anchoredPosition is in the local space of _mapContent.
        // The parent's rotation (+playerYaw) will rotate the icon into
        // world position. We use the player's position as the origin
        // (the background sprite already carries the shift toward
        // _mapCenter, so the marker delta is just marker - player).
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            Vector3 d = t.Marker.WorldTransform.position - _playerTransform.position;
            float rx = d.x * _pxPerMeter;
            float rz = d.z * _pxPerMeter;
            float distancePixels = Mathf.Sqrt(rx * rx + rz * rz);

            if (distancePixels <= _mapPixelRadius)
            {
                if (t.Icon != null)
                {
                    t.Icon.gameObject.SetActive(true);
                    t.Icon.anchoredPosition = new Vector2(rx, rz);
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
