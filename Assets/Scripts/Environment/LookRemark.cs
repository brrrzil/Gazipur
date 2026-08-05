using UnityEngine;
using Zenject;
using static EnumData;

// Universal 'look at object' remark trigger. Fires the configured
// RemarksType when the player keeps the crosshair on this
// GameObject's Renderer for more than _lookDuration seconds while
// within _lookDistance metres. The component is placed directly
// on the target GameObject (the one that has the MeshRenderer -
// [RequireComponent] enforces it). The pond case is the original
// use (soMuchWater remark), but the same component can be dropped
// on any other GameObject with a Renderer to trigger a different
// remark - the Inspector fields drive the configuration per
// instance, no code changes needed.
//
// Camera.main is used for the raycast (centre-of-screen =
// crosshair direction). Only TerrainCollider hits block the
// visibility - walls, fences, trees, items, the Player itself
// all do not.
[RequireComponent(typeof(Renderer))]
public class LookRemark : MonoBehaviour
{
    [SerializeField] private float _lookDistance = 25.0f;
    [SerializeField] private float _lookDuration = 1.0f;
    [SerializeField] private RemarksType _remarkType = RemarksType.soMuchWater;

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;

    private float _lookTimer;
    private bool _hasFired;
    private bool _initialised;
    private Renderer _renderer;
    private Camera _camera;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _camera = Camera.main;
        _initialised = _renderer != null
            && _camera != null
            && _dialog != null
            && _quest != null
            && _dialog.Remarks != null;
    }

    private void Update()
    {
        if (!_initialised || _hasFired) return;

        if (_quest.QuestsState.TryGetValue(Quests.filter, out int filterState) && filterState == 2) return;

        Ray ray = _camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        if (!_renderer.bounds.IntersectRay(ray, out float dist) || dist > _lookDistance)
        {
            _lookTimer = 0f;
            return;
        }

        // Terrain-only blocker: if the ray hits a terrain collider
        // between the camera and the object, the object is not
        // visible. Walls, fences, items, the Player itself - all
        // ignored, the player 'sees' the object through them.
        if (Physics.Raycast(ray, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider is TerrainCollider)
        {
            _lookTimer = 0f;
            return;
        }

        _lookTimer += Time.deltaTime;
        if (_lookTimer >= _lookDuration)
        {
            _dialog.Remarks.StartRemark(_remarkType);
            Debug.Log($"[LookRemark] {gameObject.name} -> {_remarkType}");
            _hasFired = true;
        }
    }

    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFired = false;
    }
}
