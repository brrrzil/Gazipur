using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 5.0f;
    private const float _lookDuration = 1.0f;
    private const float _lookHalfAngleDeg = 30.0f;
    private const float _statusLogInterval = 1.0f;

    [SerializeField] private List<GameObject> _ponds = new List<GameObject>();
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private bool _drawDebug = true;

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

        string camName = _cameraTransform != null ? _cameraTransform.name : "null";
        int pondCount = _ponds != null ? _ponds.Count : 0;
        Debug.Log("[PondLookRemark] Awake: camera=" + camName + " pondsFound=" + pondCount + " initialised=" + _initialised);

        if (_ponds != null)
        {
            for (int i = 0; i < _ponds.Count; i++)
            {
                GameObject p = _ponds[i];
                if (p == null) continue;
                Debug.Log("[PondLookRemark]   pond[" + i + "] = " + p.name + " at " + p.transform.position);
            }
        }
    }

    private void Update()
    {
        _frameCount++;

        if (!_initialised)
        {
            if (Time.time - _lastStatusLogTime >= _statusLogInterval)
            {
                _lastStatusLogTime = Time.time;
                string camName = _cameraTransform != null ? _cameraTransform.name : "null";
                int pondCount = _ponds != null ? _ponds.Count : 0;
                Debug.LogError("[PondLookRemark] not initialised: camera=" + camName + " ponds=" + pondCount +
                    ". Add a Camera.main in the scene, or wire _cameraTransform / _ponds in the Inspector.");
            }
            return;
        }

        if (_hasFiredSoMuchWater) return;

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

            // Distance to the pond's mesh surface, not its transform
            // position (transform.position is the GameObject pivot which
            // for a flat pond sits at the centre of the water surface,
            // so a player standing on the bank is 5+ metres from the
            // pivot even when they are clearly at the pond). ClosestPoint
            // on the MeshRenderer's world-space AABB returns the nearest
            // point on the mesh bounds - if the player is inside the
            // bounds it returns camPos (distance 0), if they are on the
            // bank it returns the nearest edge of the water, if they are
            // far it returns the nearest corner.
            Renderer rend = pond.GetComponentInChildren<Renderer>();
            float dist;
            if (rend != null)
            {
                dist = Vector3.Distance(camPos, rend.bounds.ClosestPoint(camPos));
            }
            else
            {
                dist = (pond.transform.position - camPos).magnitude;
            }
            if (dist > _lookDistance) continue;

            if (dist < closestInRangeDist)
            {
                closestInRangeDist = dist;
                closestInRange = pond;
            }

            // Angle check still uses transform.position as the look target
            // because ClosestPoint collapses to camPos when the player
            // is inside the bounds (zero-length direction = undefined
            // dot product). The pond centre is the most stable 'aim at
            // this thing' target for first-person looking.
            Vector3 toCentre = pond.transform.position - camPos;
            float centreDist = toCentre.magnitude;
            if (centreDist < 0.001f)
            {
                // Player is right at the pond centre - treat as looking at it.
                isLookingAtPond = true;
                nearestPond = pond;
                nearestDist = dist;
                if (_drawDebug) Debug.DrawRay(camPos, camForward * 0.5f, Color.green, 0.1f);
                continue;
            }
            Vector3 dir = toCentre / centreDist;
            float dot = Vector3.Dot(camForward, dir);
            if (dot < cosHalfAngle) continue;

            if (_drawDebug) Debug.DrawRay(camPos, dir * dist, Color.green, 0.1f);
            if (dist < nearestDist) { nearestDist = dist; nearestPond = pond; }
            isLookingAtPond = true;
        }

        if (!isLookingAtPond && _drawDebug && closestInRange != null)
        {
            Renderer rend = closestInRange.GetComponentInChildren<Renderer>();
            Vector3 targetPoint;
            if (rend != null)
            {
                targetPoint = rend.bounds.ClosestPoint(camPos);
            }
            else
            {
                targetPoint = closestInRange.transform.position;
            }
            Debug.DrawRay(camPos, (targetPoint - camPos), Color.red, 0.1f);
        }

        if (Time.time - _lastStatusLogTime >= _statusLogInterval)
        {
            _lastStatusLogTime = Time.time;
            // Always log the absolute nearest pond (no range filter) plus
            // the player position, so the user can see "I am at X, nearest
            // pond is at Y, distance Z m" even when Z > 5 m. Distance
            // here is also measured to the mesh bounds, not the transform
            // position, so the user can see "I am on the bank" vs
            // "I am at the centre".
            GameObject absNearest = null;
            float absNearestDist = float.PositiveInfinity;
            for (int i = 0; i < _ponds.Count; i++)
            {
                GameObject pond = _ponds[i];
                if (pond == null) continue;
                Renderer rend = pond.GetComponentInChildren<Renderer>();
                float d;
                if (rend != null)
                {
                    d = Vector3.Distance(camPos, rend.bounds.ClosestPoint(camPos));
                }
                else
                {
                    d = (pond.transform.position - camPos).magnitude;
                }
                if (d < absNearestDist)
                {
                    absNearestDist = d;
                    absNearest = pond;
                }
            }
            string absName = absNearest != null ? absNearest.name : "<none>";
            string absDist = absNearest != null ? absNearestDist.ToString("F2") + "m" : "n/a";

            if (isLookingAtPond)
            {
                string pondName = nearestPond != null ? nearestPond.name : "null";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " LOOKING at " + pondName +
                    " dist=" + nearestDist.ToString("F2") + "m timer=" + _lookTimer.ToString("F2") + "/" + _lookDuration.ToString("F2") +
                    "s | player=" + camPos.ToString("F1") +
                    " absNearest=" + absName + " absDist=" + absDist);
            }
            else
            {
                string nearestName = closestInRange != null ? closestInRange.name : "<none in 5m>";
                string distStr = closestInRange != null ? closestInRangeDist.ToString("F2") + "m" : "n/a";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " idle in-5m=" + nearestName +
                    " dist=" + distStr + " | player=" + camPos.ToString("F1") +
                    " absNearest=" + absName + " absDist=" + absDist +
                    " timer=" + _lookTimer.ToString("F2"));
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
