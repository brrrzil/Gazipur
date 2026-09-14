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
    [Tooltip("Size of the location in world metres (X, Z). The map sprite represents exactly this rectangle. For a 270x270 m location, set to (270, 270). The player walks through this rectangle and the map shows their position by projecting world metres to sprite pixels.")]
    [SerializeField] private Vector2 _mapWorldSize = new Vector2(270f, 270f);
    [Tooltip("Size of the MapBackground sprite in pixels (X, Y). Must match the sprite's source asset. For a 1024x823 sprite, set to (1024, 823). The map sprite IS the location, stretched over _mapWorldSize metres.")]
    [SerializeField] private Vector2 _mapPixelSize = new Vector2(1024f, 823f);
    [Tooltip("World position used as the map's origin. The sprite's bottom-left corner in local space corresponds to this world point. If left at (0, 0, 0), the script auto-uses the player's position when the map is first opened so the player starts at the centre of the visible mask regardless of where they spawned. Set to a specific world point (eg the literal corner of the location) if you want to fix the projection.")]
    [SerializeField] private Vector3 _mapCenter = Vector3.zero;

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
        // Only auto-compute _mapPixelRadius on the very first OnEnable
        // (the default value of 150 is the sentinel). After the user has
        // set a value in the Inspector, respect it - even across runs,
        // since Unity serialises the value. This lets the user tune the
        // radius to control which markers get the GTA-style rim arrow.
        if (_mapPixelRadius <= 0f && _mapMask != null && _mapMask.sizeDelta.x > 0f)
            _mapPixelRadius = _mapMask.sizeDelta.x * 0.5f;
        // pxPerMeter is derived directly from the location size and
        // the sprite size. The sprite IS the location - 1 metre of
        // world distance maps to exactly (_mapPixelSize / _mapWorldSize)
        // pixels on the sprite.
        _pxPerMeter = new Vector2(
            _mapPixelSize.x / Mathf.Max(0.01f, _mapWorldSize.x),
            _mapPixelSize.y / Mathf.Max(0.01f, _mapWorldSize.y));
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

    private bool _initialCenterSet;

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

        // Auto-centre: if the user left _mapCenter at (0, 0, 0), treat
        // it as 'unset' and lock it to the player's current world
        // position the first time the map is open. This way the player
        // always appears at the centre of the mask regardless of
        // where they spawned, and walking the player keeps the world
        // map drifting around them. The user can override by setting
        // _mapCenter to a non-zero world point - in that case the
        // player will appear off-centre when they walk away from it.
        if (!_initialCenterSet && _mapCenter == Vector3.zero)
        {
            _mapCenter = _playerTransform.position;
            _initialCenterSet = true;
        }

        Vector3 playerDelta = _playerTransform.position - _mapCenter;
        float playerYaw = _playerTransform.eulerAngles.y;

        // Direct projection: the sprite IS the location, and the
        // player's world position is mapped to the sprite's local
        // pixel position via _pxPerMeter. The sprite shifts the
        // opposite direction of the player so the player stays at the
        // centre of the mask. No 'visible world radius' or extra
        // scaling is involved - changing _mapWorldSize or _mapPixelSize
        // just changes the zoom factor of the map (how many metres per
        // pixel), nothing else.
        //
        // Clamp the shift to the sprite's half-size so the sprite
        // never disappears entirely from behind the mask. Beyond the
        // clamp the sprite stops moving and the player effectively
        // 'walks off the edge' of the map - that is the correct
        // behaviour when the player leaves the location represented by
        // the sprite.
        float maxShiftX = _mapPixelSize.x * 0.5f;
        float maxShiftY = _mapPixelSize.y * 0.5f;
        float shiftX = Mathf.Clamp(-playerDelta.x * _pxPerMeter.x, -maxShiftX, maxShiftX);
        float shiftY = Mathf.Clamp(-playerDelta.z * _pxPerMeter.y, -maxShiftY, maxShiftY);

        // _mapContent stays at the centre of MapMask (Awake forced the
        // pivot and anchoredPosition). Its rotation pivots around that
        // centre, which is also the centre of the radar where the
        // player arrow sits. Movement is delegated to _mapBackground.
        _mapContent.localPosition = Vector3.zero;
        // The map sprite is drawn with a perspective baked in, so it
        // must NOT rotate. Player facing direction is conveyed by
        // rotating the arrow, not the map.
        _mapContent.localRotation = Quaternion.identity;
        // Scale-based zoom: scale the entire map subtree so icons and
        // background both grow together. The player's world delta is
        // mapped to the same number of pixels regardless of zoom, so
        // the player stays centred and the world does not appear to
        // 'pan faster' at higher zoom.
        if (_zoom > 0f) _mapContent.localScale = new Vector3(_zoom, _zoom, 1f);

        // Rotate the player arrow to show the facing direction. The
        // arrow is a child of _mapMask (not _mapContent) so it is not
        // affected by the zoom scale; we apply the rotation directly
        // in MapMask local space. -playerYaw because the arrow sprite
        // is typically drawn pointing up (+Y), and we want it to point
        // 'up' when the player faces the sprite's 'up' direction; a
        // positive playerYaw (player turns east) should rotate the
        // arrow clockwise on the screen.
        if (_playerArrow != null)
            _playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerYaw + _playerArrowBaseAngle);

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
            float rx = d.x * _pxPerMeter.x;
            float rz = d.z * _pxPerMeter.y;
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
                "Select an empty GameObject placed at the visual centre of the location, " +
                "then right-click the MapUI component and pick this menu item.");
            return;
        }
        _mapCenter = go.transform.position;
        Debug.Log($"[MapUI] _mapCenter set to {_mapCenter} (from selected object '{go.name}').");
#endif
    }
}
