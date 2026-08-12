using UnityEngine;
using Zenject;
using static EnumData;

// Universal 'look at object' remark trigger. Fires the configured
// RemarksType when the player keeps the crosshair on this
// GameObject's Collider for more than _lookDuration seconds while
// within _lookDistance metres. The component is placed on a
// dedicated trigger object (a separate GameObject with a Collider
// component, typically isTrigger = true, often invisible - a
// MeshCollider-only 'RemarkTrigger' object that defines the
// 'look at this volume' zone for the remark).
//
// Two design choices to be aware of:
//
// 1. The component reads the Collider's AABB, not the Renderer's
// AABB. This is the right choice for trigger objects because
// trigger objects often have NO Renderer (they are invisible
// volumes that only exist to detect look-at). The [RequireComponent
// (typeof(Collider))] attribute enforces that the GameObject has
// at least one Collider, which is also the natural choice for a
// 'look at volume' - the volume is defined by a BoxCollider,
// SphereCollider, or MeshCollider.
//
// 2. The terrain blocker check uses QueryTriggerInteraction.Ignore
// so the RemarkTrigger's own isTrigger = true collider does NOT
// block the raycast (it is the target, not a blocker). Terrain
// (isTrigger = false, type TerrainCollider) DOES block, so the
// player cannot fire the remark by looking through a hill at
// the trigger behind it.
[RequireComponent(typeof(Collider))]
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
    private Collider _collider;
    private Camera _camera;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _camera = Camera.main;
        _initialised = _collider != null
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

        if (!_collider.bounds.IntersectRay(ray, out float dist) || dist > _lookDistance)
        {
            _lookTimer = 0f;
            return;
        }

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
