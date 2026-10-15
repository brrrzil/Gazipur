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
    [Tooltip("Pixel radius in _mapMask local space. When a clamp icon's distance from the mask centre exceeds this, the icon switches into arrow mode: its Image swaps to _clampArrowSprite, its RectTransform snaps to the rim (20x20) in the original direction, and its z-rotation points away from the centre. When the icon comes back inside the radius, the original sprite, original sizeDelta and identity rotation are restored and the icon returns to its hand-picked anchoredPosition.")]
    [SerializeField] private float _clampRadius = 100f;
    [Tooltip("Sprite used while a clamp icon sits beyond _clampRadius (the off-radar arrow).")]
    [SerializeField] private Sprite _clampArrowSprite;
    [Tooltip("Hand-placed icons inside MapMask / MarkersParent. Each entry is a single RectTransform whose sprite, sizeDelta and rotation are swapped at runtime depending on its distance to the mask centre.")]
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

    // Hand-placed map icon: a single RectTransform the user wired
    // in the Inspector. The icon has two visual modes:
    //
    //   - Inside _clampRadius (default): the icon's own sprite,
    //     own sizeDelta, identity rotation and hand-picked
    //     anchoredPosition are shown. This is what the user
    //     composed in the Editor.
    //
    //   - Beyond _clampRadius: the icon morphs into an arrow —
    //     its Image swaps to _clampArrowSprite, its sizeDelta
    //     becomes (20, 20), its z-rotation points away from the
    //     mask centre and its anchoredPosition is clamped to
    //     length _clampRadius in the original direction.
    //
    // When the player walks back inside the radius, the icon
    // restores its original sprite / size / rotation / position.
    [System.Serializable]
    private class ClampIcon
    {
        public RectTransform Icon;

        // Cached at Start: the image and the values to restore
        // when the icon comes back inside _clampRadius.
        [System.NonSerialized] public Image Image;
        [System.NonSerialized] public Sprite OriginalSprite;
        [System.NonSerialized] public Vector2 OriginalSizeDelta;
        [System.NonSerialized] public Vector2 OriginalAnchoredPos;
        [System.NonSerialized] public RectTransform IconParentRt;
        [System.NonSerialized] public bool HasIcon;
        [System.NonSerialized] public bool HasImage;
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
        // Capture each icon's hand-picked sprite / size / position
        // so we can restore them when the player walks back inside
        // _clampRadius. The icon's parent RectTransform is cached
        // as well — its mask-local offset is re-evaluated each
        // frame because the parent chain (MarkersParent inside
        // MapBackground inside MapMask) is panned every frame by
        // the sprite-pan line.
        if (_clampIcons == null) return;
        for (int i = 0; i < _clampIcons.Length; i++)
        {
            var ci = _clampIcons[i];
            if (ci == null || ci.Icon == null) continue;
            ci.HasIcon = true;
            ci.IconParentRt = ci.Icon.parent as RectTransform;
            ci.OriginalAnchoredPos = ci.Icon.anchoredPosition;
            ci.OriginalSizeDelta = ci.Icon.sizeDelta;
            ci.Image = ci.Icon.GetComponent<Image>();
            ci.HasImage = ci.Image != null;
            if (ci.HasImage) ci.OriginalSprite = ci.Image.sprite;
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
        // Each icon is a single RectTransform. Every frame we
        // re-measure its offset from the mask centre (ToMaskLocal
        // already includes the parent's sprite-pan, so this stays
        // correct as the player walks around).
        //
        //   - dist <= _clampRadius: icon mode. The icon's own
        //     sprite, sizeDelta, identity rotation and
        //     hand-picked anchoredPosition are restored.
        //
        //   - dist > _clampRadius: arrow mode. The icon's Image
        //     swaps to _clampArrowSprite, its sizeDelta becomes
        //     (20, 20), its z-rotation points away from the
        //     centre and its anchoredPosition is clamped to
        //     length _clampRadius in the original direction. The
        //     icon itself stays visible — it just *becomes* the
        //     arrow.
        float nearestDist = float.PositiveInfinity;
        for (int i = 0; i < _clampIcons.Length; i++)
        {
            var ci = _clampIcons[i];
            if (!ci.HasIcon) continue;

            Vector2 iconMaskLocal = ToMaskLocal(ci.Icon);
            float dist = iconMaskLocal.magnitude;
            if (dist < nearestDist) nearestDist = dist;

            if (dist <= _clampRadius)
            {
                if (ci.Icon.sizeDelta != ci.OriginalSizeDelta)
                    ci.Icon.sizeDelta = ci.OriginalSizeDelta;
                if (ci.Icon.localRotation != Quaternion.identity)
                    ci.Icon.localRotation = Quaternion.identity;
                if (ci.Icon.anchoredPosition != ci.OriginalAnchoredPos)
                    ci.Icon.anchoredPosition = ci.OriginalAnchoredPos;
                if (ci.HasImage && ci.Image.sprite != ci.OriginalSprite)
                    ci.Image.sprite = ci.OriginalSprite;
            }
            else
            {
                var dir = iconMaskLocal / dist;
                Vector2 rimMaskLocal = dir * _clampRadius;
                Vector2 iconParentMask = ci.IconParentRt != null
                    ? ToMaskLocal(ci.IconParentRt)
                    : Vector2.zero;
                ci.Icon.anchoredPosition = rimMaskLocal - iconParentMask;
                ci.Icon.sizeDelta = new Vector2(20f, 20f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                ci.Icon.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (ci.HasImage && _clampArrowSprite != null && ci.Image.sprite != _clampArrowSprite)
                    ci.Image.sprite = _clampArrowSprite;
            }
        }
        Debug.Log($"[MapUI] nearest clamp icon: {nearestDist:F1}px from player arrow");
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
