using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

// Fires the 'soMuchWater' remark when the player keeps their crosshair on
// a pond for more than 1 second while standing within 5 metres of it.
// Skips the remark after the filter has been built (QuestsState[Quests.filter]==2).
public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 5.0f;
    private const float _lookDuration = 1.0f;
    private const float _lookHalfAngleDeg = 30.0f;
    private const float _statusLogInterval = 1.0f; // seconds between diagnostic logs

    [SerializeField] private List<GameObject> _ponds = new List<GameObject>();
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private bool _drawDebug = true; // default ON for round 88 v6 diagnostics

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;

    private float _lookTimer;
    private bool _hasFiredSoMuchWater;
    private bool _initialised;
    private float _lastStatusLogTime;
    private int _frameCount;

    private void Awake()
    {
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        if (_ponds == null || _ponds.Count == 0)
        {
            _ponds = new List<GameObject>();
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null) continue;
                if (!go.name.Contains("Pond")) continue;
                _ponds.Add(go);
            }
        }

        _initialised = (_cameraTransform != null) && (_ponds != null && _ponds.Count > 0);

        // Round 88 v6: log immediately on Awake so the user sees component
        // status in the Console even if Update never logs (component disabled,
        // scene not loaded, etc).
        Debug.Log($"[PondLookRemark] Awake: camera={(_cameraTransform != null ? _cameraTransform.name : \"<null>\")} " +
                  $"pondsFound={(_ponds != null ? _ponds.Count : 0)} initialised={_initialised}");
        if (_ponds != null && _ponds.Count > 0)
        {
            for (int i = 0; i < _ponds.Count; i++)
            {
                Debug.Log($"[PondLookRemark]   pond[{i}] = {(_ponds[i] != null ? _ponds[i].name : \"<null>\")} " +
                          $"at {(_ponds[i] != null ? _ponds[i].transform.position.ToString() : \"?\")}");
            }
        }
    }

    private void Update()
    {
        _frameCount++;

        if (!_initialised)
        {
            // Round 88 v6: in v4/v5 we silently no-op'd if the component
            // did not find a camera or any pond in Awake. That made
            // debugging impossible - the user saw nothing in the Console
            // and had no way to know whether the issue was the component
            // not wired, the scene not loaded yet, the auto-find filter
            // missing the ponds, etc. v6 logs every second so the user
            // can see exactly what is wrong.
            if (Time.time - _lastStatusLogTime >= _statusLogInterval)
            {
                _lastStatusLogTime = Time.time;
                Debug.LogError($"[PondLookRemark] not initialised in Update: " +
                               $"camera={(_cameraTransform != null ? _cameraTransform.name : \"<null>\")} " +
                               $"ponds={(_ponds != null ? _ponds.Count : 0)}. " +
                               $"Check that the GameObject is in the active scene, has a Camera.main " +
                               $"(GameScene has 'Main Camera' with tag MainCamera), and that the ponds " +
                               $"are named with 'Pond' in the name (e.g. DirtyPond, ClearPond, " +
                               $"PoisonedPond, *PondSurface). If still failing, drag the ponds into " +
                               $"the _ponds list in the Inspector explicitly.");
            }
            return;
        }

        if (_hasFiredSoMuchWater)
        {
            return;
        }

        // Skip after filter built.
        bool filterBuilt = false;
        if (_quest != null
            && _quest.QuestsState != null
            && _quest.QuestsState.TryGetValue(Quests.filter, out int filterState)
            && filterState == 2)
        {
            filterBuilt = true;
        }
        if (filterBuilt) return;

        Vector3 camPos = _cameraTransform.position;
        Vector3 camForward = _cameraTransform.forward;
        float cosHalfAngle = Mathf.Cos(_lookHalfAngleDeg * Mathf.Deg2Rad);

        bool isLookingAtPond = false;
        float nearestDist = float.PositiveInfinity;
        GameObject nearestPond = null;
        GameObject closestInRange = null;
        float closestInRangeDist = float.PositiveInfinity;

        for (int i = 0; i < _ponds.Count; i++)
        {
            GameObject pond = _ponds[i];
            if (pond == null) continue;

            Vector3 toPond = pond.transform.position - camPos;
            float dist = toPond.magnitude;
            if (dist > _lookDistance) continue;

            if (dist < closestInRangeDist)
            {
                closestInRangeDist = dist;
                closestInRange = pond;
            }

            if (dist < 0.001f) continue;
            Vector3 dir = toPond / dist;
            float dot = Vector3.Dot(camForward, dir);
            if (dot < cosHalfAngle) continue;

            if (_drawDebug) Debug.DrawRay(camPos, dir * dist, Color.green, 0.1f);
            if (dist < nearestDist) { nearestDist = dist; nearestPond = pond; }
            isLookingAtPond = true;
        }

        if (!isLookingAtPond && _drawDebug && closestInRange != null)
        {
            Vector3 toClosest = closestInRange.transform.position - camPos;
            Debug.DrawRay(camPos, toClosest.normalized * closestInRangeDist, Color.red, 0.1f);
        }

        // Round 88 v6: periodic status log so the user can see the
        // component is alive and what state it is in even when not
        // looking at a pond. Once per second is enough to confirm
        // 'it is running' without flooding the Console.
        if (Time.time - _lastStatusLogTime >= _statusLogInterval)
        {
            _lastStatusLogTime = Time.time;
            if (isLookingAtPond)
            {
                Debug.Log($"[PondLookRemark] frame={_frameCount} LOOKING at {nearestPond?.name} " +
                          $"dist={nearestDist:F2}m timer={_lookTimer:F2}/{_lookDuration:F2}s " +
                          $"camera={_cameraTransform.name} camPos={camPos}");
            }
            else
            {
                string nearest = closestInRange != null ? closestInRange.name : \"<none in range>\";
                string distStr = closestInRange != null ? closestInRangeDist.ToString(\"F2\") + \"m\" : \"n/a\";
                Debug.Log($"[PondLookRemark] frame={_frameCount} idle nearest={nearest} dist={distStr} " +
                          $"timer={_lookTimer:F2} camera={_cameraTransform.name} camPos={camPos}");
            }
        }

        if (isLookingAtPond)
        {
            _lookTimer += Time.deltaTime;
            if (_lookTimer >= _lookDuration)
            {
                if (_dialog != null && _dialog.Remarks != null)
                {
                    _dialog.Remarks.StartRemark(RemarksType.soMuchWater);
                }
                _hasFiredSoMuchWater = true;
            }
        }
        else
        {
            _lookTimer = 0f;
        }
    }

    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFiredSoMuchWater = false;
    }
}
