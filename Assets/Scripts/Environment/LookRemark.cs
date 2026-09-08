using UnityEngine;
using Zenject;
using static EnumData;

[RequireComponent(typeof(Collider))]
public class LookRemark : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Seconds after enable before the remark can fire. Prevents the remark from triggering in the first frame when the player's spawn-point camera direction happens to aim at the trigger.")]
    [SerializeField] private float _activationDelay = 1.5f;
    [Tooltip("Min distance the player must move from spawn before the remark can fire. 0 disables. Prevents the remark from triggering if the player spawns already inside the look volume.")]
    [SerializeField] private float _minMoveDistance = 2f;

    [Header("Remark")]
    [SerializeField] private float _lookDistance = 25.0f;
    [SerializeField] private float _lookDuration = 1.0f;
    [SerializeField] private RemarksType _remarkType = RemarksType.soMuchWater;

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;
    [Inject] private PlayerMovement _movement;

    private float _lookTimer;
    private bool _hasFired;
    private bool _initialised;
    private float _enabledTime;
    private Vector3 _spawnPosition;
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
            && _dialog.Remarks != null
            && _movement != null;
    }

    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFired = false;
        _enabledTime = Time.unscaledTime;
        if (_movement != null) _spawnPosition = _movement.transform.position;
    }

    private void Update()
    {
        if (!_initialised || _hasFired) return;

        // Wait for the activation delay to elapse before checking anything.
        if (Time.unscaledTime - _enabledTime < _activationDelay) return;

        // Require the player to have moved at least _minMoveDistance from spawn.
        if (_minMoveDistance > 0f
            && _movement != null
            && Vector3.Distance(_movement.transform.position, _spawnPosition) < _minMoveDistance)
            return;

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
}
