using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Zenject;

public class MapUI : MonoBehaviour
{
    public static MapUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Optional. The root GameObject that wraps the whole minimap hierarchy. If set, the map is hidden / shown via SetActive on this GameObject.")]
    [SerializeField] private GameObject _mapRoot;
    [Tooltip("Parent RectTransform. The map sprite and marker icons live under this. Its position / rotation / pivot / anchor are configured entirely in the Inspector (RectTransform). The script does NOT touch its position or rotation - the user sets those.")]
    [SerializeField] private RectTransform _mapContent;
    [Tooltip("Optional. Parent of MapMask. The mask clips the map to a circle.")]
    [SerializeField] private RectTransform _mapMask;
    [Tooltip("Player arrow icon. Should be a child of _mapMask and sit at the centre (anchoredPosition (0, 0)). Its rotation tracks the player's facing direction.")]
    [SerializeField] private RectTransform _playerArrow;
    [Tooltip("RectTransform of the rectangular map sprite. Its anchoredPosition / pivot / size are configured in the Inspector (RectTransform). The user drags the sprite to the right position - the script does NOT touch its anchoredPosition.")]
    [SerializeField] private RectTransform _mapBackground;
    [Tooltip("Parent for spawned marker icons. Should be a child of _mapContent.")]
    [SerializeField] private RectTransform _markersParent;
    [Tooltip("Optional. Parent for marker arrows.")]
    [SerializeField] private RectTransform _markersEdgeParent;
    [Tooltip("Prefab for a single marker icon.")]
    [SerializeField] private RectTransform _markerIconPrefab;
    [Tooltip("Prefab for an edge-pointing arrow.")]
    [SerializeField] private RectTransform _markerArrowPrefab;

    [Header("Zoom")]
    [Tooltip("How many sprite pixels correspond to one metre of world distance. For a sprite drawn at the same scale as the world (1 m of world distance = 10 px on the sprite), set to 10. The map zoom is controlled entirely by this single number.")]
    [SerializeField] private float _pixelsPerMeter = 10f;

    [Header("Player Arrow")]
    [Tooltip("Extra degrees added to the player arrow rotation. Default 0. Set this if the arrow sprite is not drawn pointing 'up' (eg if it points right, set 90 so it points up when the player faces north).")]
    [SerializeField] private float _playerArrowBaseAngle = 0f;

    [Header("Persistence")]
    [Tooltip("PlayerPrefs key prefix for 'marker collected' state. The full key is _collectedPrefix + marker.Id.")]
    [SerializeField] private string _collectedPrefix = "map_marker_collected_";

    private readonly List<Graphic> _graphics = new List<Graphic>();
    private readonly List<Behaviour> _behavioursToToggle = new List<Behaviour>();
    private readonly List<TrackedMarker> _markers = new List<TrackedMarker>();
    private Transform _playerTransform;
    private bool _isOpen;
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
        SetOpen(false);
    }

    private void OnEnable()
    {
        // Auto-compute _mapPixelRadius from the mask size on the
        // first OnEnable (sentinel value 150 or below = unset).
        // The user can override by setting a positive value.
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
        if (_mapPixelRadius <= 0f && _mapMask != null)
            _mapPixelRadius = _mapMask.rect.width * 0.5f;

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

        float playerYaw = _playerTransform.eulerAngles.y;

        // The sprite does NOT move - it sits at whatever position the
        // user configured in the Inspector (RectTransform). The script
        // only handles:
        //   1. The player arrow rotation, which tracks the player's
        //      facing direction (-playerYaw + baseAngle).
        //   2. The marker icon positions, which are computed as the
        //      delta between the marker and the player in world space,
        //      scaled by _pixelsPerMeter. This is the GTA-style 'cursor
        //      stays in the centre of the radar, markers move around
        //      it' projection.

        // Rotate the player arrow to show the facing direction.
        if (_playerArrow != null)
            _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerYaw + _playerArrowBaseAngle);

        // Markers: positioned by their offset from the player.
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            Vector3 d = t.Marker.WorldTransform.position - _playerTransform.position;
            float rx = d.x * _pixelsPerMeter;
            float rz = d.z * _pixelsPerMeter;
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

    private float _mapPixelRadius = 150f;

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
