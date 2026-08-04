using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 5.0f;
    private const float _lookDuration = 1.0f;
    private const float _statusLogInterval = 1.0f;

    [SerializeField] private List<GameObject> _ponds = new List<GameObject>();
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private bool _drawDebug = true;
    [Tooltip("Layer mask for colliders that should BLOCK the look-at-pond check. " +
        "Default: nothing (no blockers, pure bounds check from v13). " +
        "Typical setup: tick only the Terrain layer here, so the ray stops at the ground " +
        "but ignores walls, fences, trees, and items (the user can still 'see' the pond " +
        "through those). The pond's own colliders are always skipped regardless of this mask.")]
    [SerializeField] private LayerMask _blockerMask = 0;

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

        Vector3 camPos;
        Vector3 rayOrigin;
        Vector3 rayDir;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            camPos = mainCam.transform.position;
            Ray screenRay = mainCam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            rayOrigin = screenRay.origin;
            rayDir = screenRay.direction;
        }
        else
        {
            camPos = _cameraTransform.position;
            rayOrigin = _cameraTransform.position;
            rayDir = _cameraTransform.forward;
        }
        Ray ray = new Ray(rayOrigin, rayDir);

        // Round 88 v14: bring back Physics.Raycast for the blocker
        // check, but ONLY for the user-specified _blockerMask layer
        // (default empty = no raycast, falling back to v13's pure
        // bounds check). The user has decided that terrain should
        // block the view of the pond (you should not be able to see
        // a pond through the ground), but other scene geometry
        // (walls, fences, trees, items) should NOT block - the
        // player knows what they are aiming at, the game does not
        // need to second-guess them with a line-of-sight test for
        // those objects.
        //
        // The expected setup is:
        //   - User sets the Terrain (or Ground) layer in the
        //     _blockerMask field in the Inspector.
        //   - All other colliders (walls, trees, items, etc) are
        //     either on a different layer that is NOT in the
        //     _blockerMask, or are on a layer that is but the user
        //     leaves the mask at 0 (default = no blockers), in
        //     which case the raycast is skipped entirely and
        //     v13's pure-bounds check is used.
        //   - The pond's own colliders (the *Surface and *Pond
        //     MeshColliders) are always skipped, regardless of the
        //     _blockerMask value, because the user explicitly
        //     reported in v12 that those were terminating the
        //     raycast prematurely.
        float nearestBlockerDist = -1f;
        GameObject nearestBlocker = null;
        if (_blockerMask.value != 0)
        {
            // Single Physics.Raycast (not RaycastNonAlloc) for the
            // blocker check is enough - we only need the closest
            // blocker, not the full list. The pond's colliders
            // are skipped by adding their layer to the inverse
            // mask, but since the pond is on the Default layer
            // (same as most of the scene), layer-based skipping
            // would also skip the very colliders we are trying
            // to detect - so we use a Physics.Raycast and then
            // filter the single hit by name in the code below.
            if (Physics.Raycast(ray, out RaycastHit blockerHit, _lookDistance, _blockerMask, QueryTriggerInteraction.Ignore))
            {
                if (!blockerHit.collider.name.Contains("Pond"))
                {
                    nearestBlockerDist = blockerHit.distance;
                    nearestBlocker = blockerHit.collider.gameObject;
                }
            }
        }

        GameObject nearestPond = null;
        float nearestDist = 0f;
        for (int i = 0; i < _ponds.Count; i++)
        {
            GameObject pond = _ponds[i];
            if (pond == null) continue;
            Renderer rend = pond.GetComponentInChildren<Renderer>();
            if (rend == null) continue;
            if (rend.bounds.IntersectRay(ray, out float boundsDist) && boundsDist <= _lookDistance)
            {
                // If there is a blocker (terrain) closer than the
                // pond's bounds intersection, the pond is not
                // visible (the player is looking at the ground
                // between themselves and the pond).
                if (nearestBlockerDist > 0f && boundsDist > nearestBlockerDist) continue;
                if (nearestPond == null || boundsDist < nearestDist)
                {
                    nearestPond = pond;
                    nearestDist = boundsDist;
                }
            }
        }

        bool isLookingAtPond = nearestPond != null;

        if (isLookingAtPond && _drawDebug)
        {
            Debug.DrawRay(camPos, rayDir * nearestDist, Color.green, 0.1f);
        }
        if (!isLookingAtPond && nearestBlockerDist > 0f && _drawDebug)
        {
            Debug.DrawRay(camPos, rayDir * nearestBlockerDist, Color.yellow, 0.1f);
        }

        GameObject closestInRange = null;
        float closestInRangeDist = float.PositiveInfinity;
        for (int i = 0; i < _ponds.Count; i++)
        {
            GameObject pond = _ponds[i];
            if (pond == null) continue;
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
        }

        if (Time.time - _lastStatusLogTime >= _statusLogInterval)
        {
            _lastStatusLogTime = Time.time;
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
            string blockerInfo = nearestBlockerDist > 0f
                ? nearestBlockerDist.ToString("F2") + "m (" + (nearestBlocker != null ? nearestBlocker.name : "?") + ")"
                : "none";

            if (isLookingAtPond)
            {
                string pondName = nearestPond != null ? nearestPond.name : "null";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " LOOKING at " + pondName +
                    " boundsDist=" + nearestDist.ToString("F2") + "m timer=" + _lookTimer.ToString("F2") + "/" + _lookDuration.ToString("F2") +
                    "s | player=" + camPos.ToString("F1") +
                    " absNearest=" + absName + " absDist=" + absDist +
                    " blockerAt=" + blockerInfo);
            }
            else
            {
                string nearestName = closestInRange != null ? closestInRange.name : "<none in 5m>";
                string distStr = closestInRange != null ? closestInRangeDist.ToString("F2") + "m" : "n/a";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " idle in-5m=" + nearestName +
                    " dist=" + distStr + " blockerAt=" + blockerInfo +
                    " | player=" + camPos.ToString("F1") +
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
