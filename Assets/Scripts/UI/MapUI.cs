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
    [Tooltip("How many sprite pixels correspond to one metre of world distance. For a 270x270 m location drawn as a 2700x2700 px sprite, set to 10 (2700 px / 270 m = 10 px/m). Walking one metre east shifts the map by 10 px west.")]
    [SerializeField] private float _pixelsPerMeter = 10f;
    [Tooltip("Size of the MapBackground sprite in pixels (X, Y). Must match the sprite's source asset. The sprite is centred on _mapCenter in world space - the script automatically figures out the sprite's bottom-left corner from the centre + pixel size + pixelsPerMeter.")]
    [SerializeField] private Vector2 _mapPixelSize = new Vector2(2700f, 2700f);
    [Tooltip("Computed: the size of the area the sprite covers in world metres. Equals (_mapPixelSize.x / _pixelsPerMeter, _mapPixelSize.y / _pixelsPerMeter). Read-only - it updates automatically whenever you change Pixels Per Meter or Map Pixel Size.")]
    [SerializeField] private Vector2 _mapWorldSize = new Vector2(540f, 540f);

    [Tooltip("World position of the CENTRE of the in-game location. For the user's 270x270 m location centred on (500, 500), set this to (500, 0, 500). The red pixel (sprite centre) should correspond to this world point.")]
    [SerializeField] private Vector3 _mapCenter = new Vector3(500f, 0f, 500f);

    [Header("Map Center helpers (Editor only)")]
    [Tooltip("Editor-only: right-click the MapUI component header in the Inspector and pick 'Set Map Center To Player Position' to capture the player's current world position into _mapCenter. Place the player at the visual centre of the location before running the game.")]
    [SerializeField] private bool _mapCenterEditorHelpers;

    [Header("Edge arrows")]
    [Tooltip("Pixel radius at which an edge-pointing arrow is shown for an important marker outside the visible map. Auto-computed from the _mapMask RectTransform (half of its size) on the first OnEnable, then preserved across runs - the user's value is not overwritten afterwards. To force re-computation, set the field to 0 in the Inspector before entering Play mode.")]
    [SerializeField] private float _mapPixelRadius = 150f;

    [Header("Rotation")]
    [Tooltip("Extra degrees added to the player arrow rotation. Default 0. Use this if the arrow sprite is drawn pointing in a direction other than 'up' (eg if it points right, set 90 so it points up when the player faces north; if it points down, set 180). The map sprite itself is NOT rotated any more - only the arrow rotates.")]
    [SerializeField] private float _playerArrowBaseAngle = 0f;

    [Header("Zoom")]
    [Tooltip("Scale factor for the entire map subtree (background sprite + marker icons). 1 = default scale. 2 = everything is twice as big (you see less of the world in the same radar area, but each visible element is larger). 0.5 = everything is half as big (you see more of the world). Set this once in the Inspector for a static scale.")]
    [SerializeField] private float _zoom = 1f;

    [Header("Persistence")]
    [Tooltip("PlayerPrefs key prefix for 'marker collected' state. The full key is _collectedPrefix + marker.Id.")]
    [SerializeField] private string _collectedPrefix = "map_marker_collected_";

    private readonly List<Graphic> _graphics = new List<Graphic>();
    private readonly List<Behaviour> _behavioursToToggle = new List<Behaviour>();
    private readonly List<TrackedMarker> _markers = new List<TrackedMarker>();
    private Transform _playerTransform;
    private bool _isOpen;
    private Vector2 _pxPerMeter;
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
        // Auto-compute _mapPixelRadius from the mask size on the
        // first OnEnable (sentinel value 150 or below = unset).
        if (_mapPixelRadius <= 0f && _mapMask != null && _mapMask.sizeDelta.x > 0f)
            _mapPixelRadius = _mapMask.sizeDelta.x * 0.5f;
        // _pxPerMeter is now a uniform scalar - the same value on
        // both X and Y. The user picks the value to match the
        // sprite-to-world ratio (eg 10 for a 2700x2700 sprite
        // over a 270x270 m location). Both sprite axes use the
        // same scale, so 1 m east = 1 m north in pixels.
        _pxPerMeter = new Vector2(_pixelsPerMeter, _pixelsPerMeter);
        // Compute _mapWorldSize from sprite pixel size and px/m so
        // the Inspector field always shows the current area covered
        // by the sprite (in metres). Read-only - the Inspector's
        // field is overwritten every time the component enables.
        RecomputeWorldSize();
    }

    private void OnValidate()
    {
        // Same recompute runs in the Editor when the user changes
        // a field - so _mapWorldSize is always up to date in the
        // Inspector even when not in Play mode.
        RecomputeWorldSize();
    }

    private void RecomputeWorldSize()
    {
        if (_pixelsPerMeter <= 0f) return;
        _mapWorldSize = new Vector2(
            _mapPixelSize.x / _pixelsPerMeter,
            _mapPixelSize.y / _pixelsPerMeter);
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

        float playerYaw = _playerTransform.eulerAngles.y;

        // Per the user's specification: the sprite is anchored at
        // its centre point O (spriteWidth/2, spriteHeight/2) in mask
        // local space. When the player is at _mapCenter in world
        // coords, the sprite is exactly centred in the mask and O is
        // at (0, 0). As the player moves:
        //   - m metres along world X axis: sprite shifts by -m * pxPerMeter
        //     in mask local X.
        //   - n metres along world Z axis: sprite shifts by -n * pxPerMeter
        //     in mask local Y (same sign as X for a GTA-style
        //     'cursor stays at the centre of the mask' projection).
        // The two signs match - both negative - so that the player
        // icon stays anchored to the centre of the mask while the
        // sprite (and the marker icons inside it) pan underneath.
        float spriteShiftX = -(_playerTransform.position.x - _mapCenter.x) * _pixelsPerMeter;
        float spriteShiftY = -(_playerTransform.position.z - _mapCenter.z) * _pixelsPerMeter;

        _mapContent.localPosition = new Vector3(spriteShiftX, spriteShiftY, 0f);
        _mapContent.localRotation = Quaternion.identity;
        if (_zoom > 0f) _mapContent.localScale = new Vector3(_zoom, _zoom, 1f);

        // Rotate the player arrow to show the facing direction. The
        // arrow is a child of _mapMask (NOT of _mapContent) so it is
        // not affected by the sprite's pan.
        if (_playerArrow != null)
            _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerYaw + _playerArrowBaseAngle);

        // The sprite itself does not need its own anchoredPosition -
        // the parent _mapContent is what pans. Markers inside
        // _mapContent follow the parent and end up at their absolute
        // world position on the map.

        // Markers are children of _mapContent so their anchoredPosition
        // is in sprite local space. With the GTA-style sprite pan,
        // markers need to be positioned relative to the player so
        // they appear at the correct offset from the cursor at the
        // centre of the mask. Marker sprite-pixel position is
        // computed from the marker-to-player delta (NOT from the
        // marker's absolute world position) - that is the standard
        // GTA-style projection.
        for (int i = 0; i < _markers.Count; i++)
        {
            var t = _markers[i];
            if (t.Marker == null) continue;
            Vector3 d = t.Marker.WorldTransform.position - _playerTransform.position;
            float rx = d.x * _pxPerMeter.x;
            float rz = d.z * _pxPerMeter.y;
            Vector2 markerSpritePx = new Vector2(rx, rz);
            float distanceFromPlayerSprite = markerSpritePx.magnitude;

            if (distanceFromPlayerSprite <= _mapPixelRadius)
            {
                if (t.Icon != null)
                {
                    t.Icon.gameObject.SetActive(true);
                    t.Icon.anchoredPosition = markerSpritePx;
                }
                if (t.Arrow != null) t.Arrow.gameObject.SetActive(false);
            }
            else
            {
                if (t.Icon != null) t.Icon.gameObject.SetActive(false);
                if (t.Arrow != null)
                {
                    t.Arrow.gameObject.SetActive(true);
                    float angle = Mathf.Atan2(markerSpritePx.y, markerSpritePx.x) * Mathf.Rad2Deg;
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

    // Inspector context menu: 'Set Map Center To Player Position'.
    // Available in the Editor at runtime - right-click the MapUI
    // component header in the Inspector. Walks the scene for a
    // PlayerMovement, reads its world position, and stores it as
    // _mapCenter. Use this when the player stands at the visual
    // centre of the location.
    [ContextMenu("Set Map Center To Player Position")]
    private void SetMapCenterToPlayerPosition()
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm == null)
        {
            Debug.LogWarning("[MapUI] No PlayerMovement in the scene - " +
                "enter Play mode and place the player at the centre of the location, " +
                "then right-click the MapUI component and pick 'Set Map Center To Player Position'.");
            return;
        }
        _mapCenter = pm.transform.position;
        Debug.Log($"[MapUI] _mapCenter set to {_mapCenter} (player world position).");
    }

    // Inspector context menu: 'Set Map Center From Selected Object'.
    // Use this in Edit mode: select a GameObject in the Hierarchy that
    // sits at the visual centre of the location (eg an empty
    // 'LocationCenter' GameObject the user drops into the scene), then
    // pick this menu item. The selected transform's world position
    // becomes _mapCenter.
    [ContextMenu("Set Map Center From Selected Object")]
    private void SetMapCenterFromSelection()
    {
#if UNITY_EDITOR
        var go = UnityEditor.Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[MapUI] No GameObject selected in the Hierarchy. " +
                "Select an empty GameObject placed at the visual bottom-left corner of the location, " +
                "then right-click the MapUI component and pick this menu item.");
            return;
        }
        _mapCenter = go.transform.position;
        Debug.Log($"[MapUI] _mapCenter set to {_mapCenter} (from selected object '{go.name}'). " +
            "This is treated as the sprite's bottom-left corner in world space.");
#endif
    }

    // Inspector context menu: 'Set Map Center To Terrain Bottom-Left'.
    // Editor-only. Picks the terrain with the largest bounds in the
    // scene and uses its bottom-left XZ corner as the sprite origin.
    // Convenient when the location is a single terrain object.
    [ContextMenu("Set Map Center To Terrain Bottom-Left")]
    private void SetMapCenterToTerrainBottomLeft()
    {
#if UNITY_EDITOR
        var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Terrain best = null;
        Bounds bestBounds = new Bounds();
        foreach (var t in terrains)
        {
            var b = t.terrainData.bounds;
            b.center += t.transform.position;
            if (best == null || b.size.sqrMagnitude > bestBounds.size.sqrMagnitude)
            {
                best = t;
                bestBounds = b;
            }
        }
        if (best == null)
        {
            Debug.LogWarning("[MapUI] No Terrain found in the scene. " +
                "Place an empty GameObject at the bottom-left corner of the location, " +
                "select it, and use 'Set Map Center From Selected Object'.");
            return;
        }
        Vector3 origin = new Vector3(bestBounds.min.x, 0f, bestBounds.min.z);
        _mapCenter = origin;
        Debug.Log($"[MapUI] _mapCenter set to {origin} (bottom-left XZ corner of terrain '{best.name}', bounds {bestBounds.size}).");
#endif
    }
}
