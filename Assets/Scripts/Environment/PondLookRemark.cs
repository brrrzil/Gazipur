using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

// Fires the 'soMuchWater' remark when the player keeps
// their crosshair on a pond for more than 1 second while
// standing within 5 metres of it. Skips the remark after
// the filter has been built (QuestsState[Quests.filter]==2).
public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 5.0f;       // metres, hardcoded per user
    private const float _lookDuration = 1.0f;      // seconds, hardcoded per user
    private const float _lookHalfAngleDeg = 30.0f;  // half-FOV of "crosshair on target" cone

    [Tooltip("Pond GameObjects. If empty, auto-find by name containing 'Pond' in Awake.")]
    [SerializeField] private List<GameObject> _ponds = new List<GameObject>();

    [Tooltip("Player camera Transform. If null, falls back to Camera.main in Awake.")]
    [SerializeField] private Transform _cameraTransform;

    [Tooltip("Draw Debug.DrawRay visualisation (green = looking at, red = in range but not aimed at).")]
    [SerializeField] private bool _drawDebug = false;

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;

    private float _lookTimer;
    private bool _hasFiredSoMuchWater;
    private bool _initialised;

    private void Awake()
    {
        // Camera fallback: Camera.main if the user did not wire _cameraTransform.
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        // Pond fallback: FindObjectsByType (Unity 2022.2+ API) filtered by name.
        // Only includes active scene GameObjects, skips prefab assets and disabled objects.
        if (_ponds == null || _ponds.Count == 0)
        {
            _ponds = new List<GameObject>();
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null) continue;
                if (!go.name.Contains("Pond")) continue;
                if (!go.scene.IsValid()) continue;       // skip prefab assets
                if (!go.activeInHierarchy) continue;    // skip disabled
                _ponds.Add(go);
            }
        }

        _initialised = (_cameraTransform != null) && (_ponds != null && _ponds.Count > 0);
    }

    private void Update()
    {
        if (!_initialised) return;
        if (_hasFiredSoMuchWater) return;

        // Skip after filter built. CompleteFilter sets QuestsState[Quests.filter]=2.
        if (_quest != null
            && _quest.QuestsState != null
            && _quest.QuestsState.TryGetValue(Quests.filter, out int filterState)
            && filterState == 2)
        {
            return;
        }

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

            // Track the closest pond in range (for red debug ray when nothing passes angle check).
            if (dist < closestInRangeDist)
            {
                closestInRangeDist = dist;
                closestInRange = pond;
            }

            if (dist < 0.001f) continue; // camera inside pond, skip angle check
            Vector3 dir = toPond / dist;
            float dot = Vector3.Dot(camForward, dir);
            if (dot < cosHalfAngle) continue;

            if (_drawDebug) Debug.DrawRay(camPos, dir * dist, Color.green);
            if (dist < nearestDist) { nearestDist = dist; nearestPond = pond; }
            isLookingAtPond = true;
        }

        if (!isLookingAtPond && _drawDebug && closestInRange != null)
        {
            Vector3 toClosest = closestInRange.transform.position - camPos;
            Debug.DrawRay(camPos, toClosest.normalized * closestInRangeDist, Color.red);
        }

        if (isLookingAtPond)
        {
            _lookTimer += Time.deltaTime;
            if (_drawDebug)
            {
                Debug.Log($"[PondLookRemark] looking at {nearestPond?.name} ({nearestDist:F2} m) timer {_lookTimer:F2} / {_lookDuration:F2} s");
            }
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

    // Round 80 v2 pattern: explicit reset for "Reload Domain off" Editor mode.
    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFiredSoMuchWater = false;
    }
}
