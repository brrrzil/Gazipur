using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MapUI : MonoBehaviour
{
    public static MapUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Optional. The root GameObject that wraps the whole minimap hierarchy (MapMask + MapContent + PlayerArrow + MarkersParent). If set, the map is hidden / shown via SetActive on this GameObject. If left empty, the map falls back to toggling Graphic.enabled on each UI element.")]
    [SerializeField] private GameObject _mapRoot;
    [Tooltip("Parent RectTransform that moves (anti-player) and rotates (player yaw) every frame. The MapBackground sprite and the MarkersParent should be children of this transform so the whole map shifts under the player.")]
    [SerializeField] private RectTransform _mapContent;
    [Tooltip("Optional. A parent RectTransform that has a circular Image + Mask component on it. The _mapContent should be a child of this transform.")]
    [SerializeField] private RectTransform _mapMask;
    [Tooltip("Player arrow icon. Should be a child of _mapMask (not of _mapContent) and sit at the centre. It does NOT rotate with the map.")]
    [SerializeField] private RectTransform _playerArrow;
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
    [Tooltip("Size of the MapBackground sprite in pixels. The whole _mapWorldSize fits inside this pixel size. For a 200x200 m location with a 2000x2000 px sprite, the ratio is 10 px / m.")]
    [SerializeField] private float _mapPixelSize = 2000f;
    [Tooltip("World position of the centre of the location. The player and all markers are measured relative to this point.")]
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
    private bool _hasRoot;

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

    public void SetOpen(bool open)
    {
        _isOpen = open;

        // Preferred path: hide the whole map subtree via SetActive. This
        // is more reliable than toggling Graphic.enabled because it also
        // skips Update / OnEnable cycles on every UI element.
        if (_hasRoot)
        {
            _mapRoot.SetActive(open);
            return;
        }

        // Fallback path: toggle individual UI elements. Used when the
        // user has not assigned _mapRoot.
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

        // Hide the marker parents during rebuild so that markers spawned
        // in a hidden state stay hidden until the map is open. The
        // parents are children of _mapContent / _mapRoot so SetActive
        // toggling on _mapRoot handles visibility for us - we just have
        // to make sure the spawned icons are not visible before the map
        // is open.
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

        if (!_playerReady)
        {
            if (_movement == null) return;
            _playerTransform = _movement.transform;
            _playerReady = true;
        }
        if (_playerTransform == null || _mapContent == null) return;

        Vector3 playerDelta = _playerTransform.position - _mapCenter;
        float playerYaw = _playerTransform.eulerAngles.y;

        // The map background sprite is _mapPixelSize wide (default 2000
        // px). The visible map circle has radius _mapPixelRadius (default
        // 150 px, half of the mask). For the background to always cover
        // the mask regardless of where the player is on the map, the
        // shift amount is clamped so that the background's edges never
        // recede past the mask's edges.
        float maxShiftX = Mathf.Max(0f, _mapPixelSize * 0.5f - _mapPixelRadius);
        float maxShiftY = Mathf.Max(0f, _mapPixelSize * 0.5f - _mapPixelRadius);

        // World-space shift we want for the background. This is what the
        // player's delta should translate to in the visible map.
        float worldShiftX = Mathf.Clamp(-playerDelta.x * _pxPerMeter, -maxShiftX, maxShiftX);
        float worldShiftZ = Mathf.Clamp(-playerDelta.z * _pxPerMeter, -maxShiftY, maxShiftY);

        // _mapContent has a local rotation of playerYaw (set below), so
        // setting localPosition = (worldShiftX, worldShiftZ) would
        // actually apply the rotation to the shift - the map would move
        // along the wrong axis when the player turns. To get the desired
        // world-space shift AFTER the rotation is applied, we apply the
        // inverse rotation to the shift here. That way, after the parent
        // rotates the shift back into world space, the result is exactly
        // (worldShiftX, worldShiftZ).
        float cos = Mathf.Cos(-playerYaw * Mathf.Deg2Rad);
        float sin = Mathf.Sin(-playerYaw * Mathf.Deg2Rad);
        _mapContent.localPosition = new Vector3(
            worldShiftX * cos - worldShiftZ * sin,
            worldShiftX * sin + worldShiftZ * cos,
            0f);

        _mapContent.localRotation = Quaternion.Euler(0f, 0f, playerYaw);

        // Markers are children of _mapContent, which is rotated by
        // playerYaw. Their anchoredPosition is in the rotated local
        // space of _mapContent, so we do NOT pre-rotate the icon
        // coordinates here. The world-space delta is converted directly
        // to pixels and assigned to anchoredPosition - the parent's
        // rotation will then rotate both the icon position and the
        // background sprite by the same amount, so they stay aligned.
        float cos = Mathf.Cos(playerYaw * Mathf.Deg2Rad);
        float sin = Mathf.Sin(playerYaw * Mathf.Deg2Rad);
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            // World-space delta of the marker relative to the player.
            Vector3 d = t.Marker.WorldTransform.position - _mapCenter - playerDelta;
            // Rotate by +playerYaw so the icon lands at the correct
            // position in the rotated local space of _mapContent. After
            // the parent's local rotation is applied, the icon appears
            // at the correct world-relative position on the visible
            // map.
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
