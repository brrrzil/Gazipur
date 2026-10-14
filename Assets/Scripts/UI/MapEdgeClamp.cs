using UnityEngine;

/// <summary>
/// Edge-clamp behavior for a static map icon. Attach to any icon
/// RectTransform that sits inside MapMask. Each frame we look at
/// the world position the icon represents, compute the pixel delta
/// from the map centre, and:
///   - if the icon fits inside the visible map radius, place it
///     at its real position (anchoredPosition = delta * pixelsPerMeter);
///   - if it falls outside, snap it to the rim of the mask and
///     rotate the (optional) child Arrow RectTransform so it points
///     back toward the real position.
///
/// The companion is <see cref="MapUI"/>, which owns _mapCenter and
/// _pixelsPerMeter and the mask radius. This script reads those
/// from the same singleton so the two paths never disagree.
///
/// Edge-clamping matches the behaviour of <see cref="MapMarker"/>'s
/// _isImportant path, so visually the dynamic and static markers
/// look the same on the rim of the map.
/// </summary>
[DisallowMultipleComponent]
public class MapEdgeClamp : MonoBehaviour
{
    [Tooltip("World position this icon represents. Set in the Inspector to match the position of the GameObject the icon is for (e.g. mother NPC, trader, skimmer).")]
    [SerializeField] private Vector3 _worldPosition;

    [Tooltip("Use GTA-style edge clamping. When false the icon is hidden if it would fall outside the mask; the MapUI version is preferred when you can attach a MapMarker to the world object directly.")]
    [SerializeField] private bool _clampToEdge = true;

    [Tooltip("If assigned, this RectTransform is rotated to point back at the real world position when the icon is clamped. Leave empty for plain icons.")]
    [SerializeField] private RectTransform _arrow;

    private RectTransform _rect;
    private Transform _playerTransform;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    private void OnEnable()
    {
        // MapUI.Instance isn't ready in Awake when scenes load in
        // strange orders; defer the first frame.
    }

    private void LateUpdate()
    {
        var ui = MapUI.Instance;
        if (ui == null) return;

        if (_playerTransform == null)
        {
            // The project has no "Player" tag in TagManager.asset, so
            // FindGameObjectWithTag("Player") always returns null and
            // the icon never moves. Find by PlayerMovement component
            // instead - same approach AimController uses (Assets/Scripts/
            // Player/AimController.cs).
            var movement = FindObjectOfType<PlayerMovement>();
            if (movement != null) _playerTransform = movement.transform;
            if (_playerTransform == null) return;
        }

        // World-space delta from the map centre, in metres.
        Vector3 dWorld = _worldPosition - ui.MapCenter;
        float mx = dWorld.x * ui.PixelsPerMeter;
        float mz = dWorld.z * ui.PixelsPerMeter;
        float dist = Mathf.Sqrt(mx * mx + mz * mz);
        float radius = ui.MaskPixelRadius;

        if (dist <= radius || !_clampToEdge)
        {
            // Inside the visible area, or no clamping requested:
            // park the icon at its real position (parent-local).
            _rect.anchoredPosition = new Vector2(mx, mz);
            if (_arrow != null) _arrow.gameObject.SetActive(false);
        }
        else
        {
            // Outside the visible area: project to the rim, point arrow inward.
            float angle = Mathf.Atan2(mz, mx) * Mathf.Rad2Deg;
            float rad = angle * Mathf.Deg2Rad;
            _rect.anchoredPosition = new Vector2(
                Mathf.Cos(rad) * radius,
                Mathf.Sin(rad) * radius);
            if (_arrow != null)
            {
                _arrow.gameObject.SetActive(true);
                _arrow.anchoredPosition = Vector2.zero;
                // Subtract 90 because the arrow sprite is drawn pointing up.
                _arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
        }
    }
}
