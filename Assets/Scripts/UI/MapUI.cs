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

    [Header("Clamp icons (static, hand-placed)")]
    [Tooltip("Pixel radius in _mapMask local space. When a clamp icon's distance from the mask centre exceeds this, the Icon is hidden and its Arrow is shown on the rim (rotated to point away from the centre). When the icon comes back inside the radius, the Icon is shown again at its hand-picked position and the Arrow hides. The icon and the arrow are two separate GameObjects; the script never touches the Icon's position, size or rotation, and never touches the Arrow's size.")]
    [SerializeField] private float _clampRadius = 150f;
    [Tooltip("Hand-placed icons. Each entry is a pair: Icon (RectTransform, static) + Arrow (RectTransform, off-radar). The Arrow is sized 20x20 in the Editor and assigned its own sprite there; the script only updates its anchoredPosition and z-rotation while it is active.")]
    [SerializeField] private ClampIcon[] _clampIcons;

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

    // Hand-placed map icon pair. The Icon is the static, hand-
    // composed picture the user wants to see when the player is
    // close to the object in the world. The Arrow is the
    // off-radar pointer the user wants to see when the player is
    // far away — sized 20x20 in the Editor with its own sprite
    // assigned there. The two are mutually exclusive at runtime:
    // when the distance from the mask centre is within
    // _clampRadius the Icon is shown and the Arrow is hidden;
    // when it exceeds _clampRadius the Icon is hidden and the
    // Arrow is parked on the rim, rotated to point away from the
    // centre. The script never touches the Icon's position, size
    // or rotation, and never touches the Arrow's size.
    [System.Serializable]
    private class ClampIcon
    {
        public RectTransform Icon;
        public RectTransform Arrow;

        // The Arrow's parent RectTransform, cached at Start. Used
        // each frame to translate the mask-local rim target into
        // the Arrow's parent's local space — the value changes
        // every frame because the Arrow's parent sits inside
        // MapBackground, which is panned by the sprite-pan line.
        [System.NonSerialized] public RectTransform ArrowParentRt;
        [System.NonSerialized] public bool HasIcon;
        [System.NonSerialized] public bool HasArrow;
    }

    private void Awake()
    {
        Instance = this;
        _hasRoot = _mapRoot != null;
        if (!_hasRoot) CollectGraphics();

        // Fallback: if the user forgot to wire up _mapBackground in
        // the Inspector, try to find a child named "MapBackground".
        // Without a reference the pan line at the bottom would silently
        // bail out and m_AnchoredPosition would never change.
        if (_mapBackground == null)
        {
            var found = transform.Find("MapBackground");
            if (found != null) _mapBackground = found as RectTransform;
            if (_mapBackground == null)
            {
                foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "MapBackground") { _mapBackground = t as RectTransform; break; }
                }
            }
        }

        SetOpen(false);
    }

    private void Start()
    {
        // Cache the Arrow's parent RectTransform and start every
        // Arrow hidden. The Update loop toggles Icon / Arrow
        // visibility based on the icon's distance to the mask
        // centre. We do not touch the Icon's position, size or
        // rotation here — those are the user's hand-picked
        // values and stay as the Editor composed them.
        if (_clampIcons == null) return;
        for (int i = 0; i < _clampIcons.Length; i++)
        {
            var ci = _clampIcons[i];
            if (ci == null || ci.Icon == null) continue;
            ci.HasIcon = true;
            if (ci.Arrow != null)
            {
                ci.ArrowParentRt = ci.Arrow.parent as RectTransform;
                ci.HasArrow = true;
                ci.Arrow.gameObject.SetActive(false);
            }
        }
    }

    // World position of a RectTransform, expressed in _mapMask
    // local space. The mask's pivot is the centre of the radar, so
    // the resulting Vector2 is the (x, y) offset from the player
    // arrow to the input rect, in canvas pixels. Re-evaluated
    // every frame because the icon's parent (MapBackground) is
    // panned by the sprite-pan line above.
    private Vector2 ToMaskLocal(RectTransform rt)
    {
        if (_mapMask == null || rt == null) return Vector2.zero;
        return (Vector2)_mapMask.InverseTransformPoint(rt.position);
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

        if (!_playerReady)
        {
            if (_movement == null) return;
            _playerTransform = _movement.transform;
            _playerReady = true;
        }
        if (_playerTransform == null || _mapBackground == null) return;

        float playerYaw = _playerTransform.eulerAngles.y;
        Vector3 d = _playerTransform.position - _mapCenter;

        // GTA-style centring on player: shift the sprite so the point
        // under the cursor (centre of the mask) is the player's world
        // position. sprite.anchoredPosition = -(player - mapCenter) * pixelsPerMeter.
        // When player == mapCenter the sprite sits at (0, 0) and the
        // cursor in the centre of the mask sees the world _mapCenter.
        // Runs every frame regardless of _isOpen so the sprite stays
        // panned to the player position even when the map is hidden.
        _mapBackground.anchoredPosition = new Vector2(-d.x * _pixelsPerMeter, -d.z * _pixelsPerMeter);

        if (_playerArrow != null)
            _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerYaw);

        if (!_isOpen) return;

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

        // Hand-placed static icons (Mama / Rahul / Skimmer).
        //
        // Each entry is an Icon + Arrow pair. The Icon is static
        // (the user's hand-picked sprite / size / rotation /
        // position). The Arrow is sized 20x20 with its own sprite
        // in the Editor. The script only does SetActive on the
        // two, plus anchoredPosition + z-rotation on the Arrow.
        //
        //   - dist <= _clampRadius: Icon visible, Arrow hidden.
        //     Icon is left exactly as the user composed it.
        //   - dist > _clampRadius: Icon hidden, Arrow visible on
        //     the rim of the mask, rotated to point away from the
        //     centre (i.e. the direction the player needs to walk).
        float nearestDist = float.PositiveInfinity;
        string nearestName = null;
        for (int i = 0; i < _clampIcons.Length; i++)
        {
            var ci = _clampIcons[i];
            if (!ci.HasIcon) continue;

            Vector2 iconMaskLocal = ToMaskLocal(ci.Icon);
            float dist = iconMaskLocal.magnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestName = ci.Icon != null ? ci.Icon.name : null;
            }

            bool iconActive = ci.Icon.gameObject.activeSelf;
            bool arrowActive = ci.HasArrow && ci.Arrow.gameObject.activeSelf;

            if (dist <= _clampRadius)
            {
                if (!iconActive) ci.Icon.gameObject.SetActive(true);
                if (ci.HasArrow && arrowActive) ci.Arrow.gameObject.SetActive(false);
            }
            else
            {
                if (iconActive) ci.Icon.gameObject.SetActive(false);
                if (ci.HasArrow)
                {
                    if (!arrowActive) ci.Arrow.gameObject.SetActive(true);
                    var dir = iconMaskLocal / dist;
                    Vector2 rimMaskLocal = dir * _clampRadius;
                    Vector2 arrowParentMask = ci.ArrowParentRt != null
                        ? ToMaskLocal(ci.ArrowParentRt)
                        : Vector2.zero;
                    ci.Arrow.anchoredPosition = rimMaskLocal - arrowParentMask;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    ci.Arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }
        if (nearestName != null)
            Debug.Log($"[MapUI] nearest clamp icon: {nearestDist:F1}px ({nearestName}) from player arrow");
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
