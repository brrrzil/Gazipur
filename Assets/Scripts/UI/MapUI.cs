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
    [Tooltip("RectTransform that hosts the rounded mask. The map background and player arrow live inside it. Position / pivot / anchor are configured in the Inspector; the script does not move it.")]
    [SerializeField] private RectTransform _mapMask;
    [Tooltip("Player arrow icon. Should be a child of _mapMask and sit at the centre (anchoredPosition (0, 0)). Rotated by -playerYaw every frame.")]
    [SerializeField] private RectTransform _playerArrow;
    [Tooltip("The image that shows the world map sprite. Its anchoredPosition IS panned at runtime: basePos - delta * pixelsPerMeter. Pivot should be (0.5, 0.5) so the sprite pans from its own centre. basePos (Inspector value) is where the sprite sits when the player is at _mapCenter.")]
    [SerializeField] private RectTransform _mapBackground;
    [Tooltip("Parent for spawned marker icons. Should be a child of _mapMask, so markers don't get pushed around by the sprite pan.")]
    [SerializeField] private RectTransform _markersParent;
    [Tooltip("Optional. Parent for marker arrows pointing at off-screen markers.")]
    [SerializeField] private RectTransform _markersEdgeParent;
    [Tooltip("Prefab for a single marker icon.")]
    [SerializeField] private RectTransform _markerIconPrefab;
    [Tooltip("Prefab for an edge-pointing arrow.")]
    [SerializeField] private RectTransform _markerArrowPrefab;

    [Header("World anchor")]
    [Tooltip("The world position at which the map sprite sits at its designed RectTransform position (the value you set in the Inspector). When the player is here, the sprite does not pan.")]
    [SerializeField] private Vector3 _mapCenter = new Vector3(500f, 0f, 500f);

    [Header("Zoom")]
    [Tooltip("How many sprite pixels correspond to one metre of world distance. 10 means 1 m = 10 sprite pixels. Increasing this zooms in (you see less area around the player).")]
    [SerializeField] private float _pixelsPerMeter = 10f;

    [Header("Persistence")]
    [Tooltip("PlayerPrefs key prefix for 'marker collected' state.")]
    [SerializeField] private string _collectedPrefix = "map_marker_collected_";

    private readonly List<Graphic> _graphics = new List<Graphic>();
    private readonly List<Behaviour> _behavioursToToggle = new List<Behaviour>();
    private readonly List<TrackedMarker> _markers = new List<TrackedMarker>();
    private Transform _playerTransform;
    private Vector2 _spriteBasePos;
    private float _mapPixelRadius = -1f;
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

        // Remember where the sprite is configured to sit in the
        // Inspector. At runtime we shift it by -(player - _mapCenter) * pxPerMeter.
        if (_mapBackground != null) _spriteBasePos = _mapBackground.anchoredPosition;

        SetOpen(false);
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
            var masks = _mapMask.GetComponentsInChildren<Mask>(true);
            _behavioursToToggle.AddRange(masks);
            var rectMasks = _mapMask.GetComponentsInChildren<RectMask2D>(true);
            _behavioursToToggle.AddRange(rectMasks);
        }
        if (_playerArrow != null)
        {
            var arrowGraphics = _playerArrow.GetComponentsInChildren<Graphic>(true);
            _graphics.AddRange(arrowGraphics);
        }
        // Note: _markersParent is rebuilt at runtime, don't pre-collect
        // its graphics. Same for _markersEdgeParent.
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
            if (_graphics[i] != null) _graphics[i].enabled = open;
        for (int i = 0; i < _behavioursToToggle.Count; i++)
            if (_behavioursToToggle[i] != null) _behavioursToToggle[i].enabled = open;
    }

    public void Toggle() => SetOpen(!_isOpen);
    public void Unlock() => SetOpen(true);

    private void RebuildMarkers()
    {
        if (_mapPixelRadius <= 0f && _mapMask != null)
            _mapPixelRadius = _mapMask.rect.width * 0.5f - 4f;

        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            var t = _markers[i];
            if (t.Icon != null) Destroy(t.Icon.gameObject);
            if (t.Arrow != null) Destroy(t.Arrow.gameObject);
        }
        _markers.Clear();

        if (_markerIconPrefab == null || _markersParent == null)
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
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            Toggle();
        if (!_isOpen) return;

        if (!_playerReady)
        {
            if (_movement == null) return;
            _playerTransform = _movement.transform;
            _playerReady = true;
        }
        if (_playerTransform == null || _mapBackground == null) return;

        float playerYaw = _playerTransform.eulerAngles.y;
        Vector3 d = _playerTransform.position - _mapCenter;

        // Sprite pan: shift the sprite's anchoredPosition by
        // -(player - mapCenter) * pixelsPerMeter so the marker at
        // _mapCenter stays under the cursor (centre of the mask).
        // basePos = whatever you set in the RectTransform Inspector.
        float dx = d.x * _pixelsPerMeter;
        float dz = d.z * _pixelsPerMeter;
        _mapBackground.anchoredPosition = new Vector2(
            _spriteBasePos.x - dx,
            _spriteBasePos.y - dz);

        // The arrow rotates only (anchoredPosition (0, 0) in the mask).
        if (_playerArrow != null)
            _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerYaw);

        // Markers and edge arrows are children of _mapMask, so their
        // anchoredPosition is delta * pixelsPerMeter directly (no need
        // to subtract sprite pan or use _mapCenter).
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            Vector3 md = t.Marker.WorldTransform.position - _playerTransform.position;
            float mx = md.x * _pixelsPerMeter;
            float mz = md.z * _pixelsPerMeter;
            float dist = Mathf.Sqrt(mx * mx + mz * mz);

            if (dist <= _mapPixelRadius)
            {
                if (t.Icon != null)
                {
                    t.Icon.gameObject.SetActive(true);
                    t.Icon.anchoredPosition = new Vector2(mx, mz);
                }
                if (t.Arrow != null) t.Arrow.gameObject.SetActive(false);
            }
            else
            {
                if (t.Icon != null) t.Icon.gameObject.SetActive(false);
                if (t.Arrow != null)
                {
                    t.Arrow.gameObject.SetActive(true);
                    float angle = Mathf.Atan2(mz, mx) * Mathf.Rad2Deg;
                    float rad = angle * Mathf.Deg2Rad;
                    t.Arrow.anchoredPosition = new Vector2(
                        Mathf.Cos(rad) * _mapPixelRadius,
                        Mathf.Sin(rad) * _mapPixelRadius);
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
