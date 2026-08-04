using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 5.0f;
    private const float _lookDuration = 1.0f;
    private const float _statusLogInterval = 1.0f;
    private const int _hitsBufferSize = 16;

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
    private RaycastHit[] _hitsBuffer = new RaycastHit[_hitsBufferSize];

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
        Ray ray = new Ray(camPos, camForward);

        // Cast along the camera forward, ignore trigger colliders. We
        // collect every collider the ray crosses in _lookDistance so
        // we can tell whether the pond is the first thing the ray
        // hits (in which case the player is 'looking at' the pond) or
        // whether some other geometry is in the way.
        int hitCount = Physics.RaycastNonAlloc(ray, _hitsBuffer, _lookDistance, ~0, QueryTriggerInteraction.Ignore);

        float nearestPondHitDist = -1f;
        GameObject nearestPondHit = null;
        float nearestBlockerDist = -1f;
        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _hitsBuffer[i].collider;
            if (c == null) continue;
            float d = _hitsBuffer[i].distance;
            if (c.name.Contains("Pond"))
            {
                if (nearestPondHitDist < 0f || d < nearestPondHitDist)
                {
                    nearestPondHitDist = d;
                    nearestPondHit = c.gameObject;
                }
            }
            else
            {
                if (nearestBlockerDist < 0f || d < nearestBlockerDist)
                {
                    nearestBlockerDist = d;
                }
            }
        }

        // The ponds in GameScene have non-convex MeshColliders
        // (verified in GameScene.unity: m_Convex: 0 on every pond
        // collider), and Unity's Physics.Raycast does not register
        // hits against non-convex MeshColliders. So the collider-
        // based check above will usually NOT find a pond hit. We
        // also intersect the ray against each pond's MeshRenderer
        // world-space AABB - if the ray pierces the AABB and the
        // nearest blocker (if any) is further than the intersection,
        // the pond is visible from the camera along the look
        // direction. This is the visual equivalent of 'I can see
        // the water surface from where I am looking'.
        float nearestBoundsDist = -1f;
        GameObject nearestBoundsPond = null;
        for (int i = 0; i < _ponds.Count; i++)
        {
            GameObject pond = _ponds[i];
            if (pond == null) continue;
            Renderer rend = pond.GetComponentInChildren<Renderer>();
            if (rend == null) continue;
            if (rend.bounds.IntersectRay(ray, out float boundsDist) && boundsDist <= _lookDistance)
            {
                if (nearestBoundsPond == null || boundsDist < nearestBoundsDist)
                {
                    nearestBoundsPond = pond;
                    nearestBoundsDist = boundsDist;
                }
            }
        }

        // Pick the closer of the two visible-pond candidates (the
        // collider-hit pond and the bounds-hit pond) as 'the pond the
        // player is looking at'. If both are blocked, neither wins.
        bool isLookingAtPond = false;
        GameObject nearestPond = null;
        float nearestDist = 0f;
        if (nearestPondHit != null && (nearestBlockerDist < 0f || nearestBlockerDist > nearestPondHitDist))
        {
            nearestPond = nearestPondHit;
            nearestDist = nearestPondHitDist;
            isLookingAtPond = true;
        }
        if (nearestBoundsPond != null && (nearestBlockerDist < 0f || nearestBlockerDist > nearestBoundsDist))
        {
            if (!isLookingAtPond || nearestBoundsDist < nearestDist)
            {
                nearestPond = nearestBoundsPond;
                nearestDist = nearestBoundsDist;
                isLookingAtPond = true;
            }
        }

        if (isLookingAtPond && _drawDebug)
        {
            Debug.DrawRay(camPos, camForward * nearestDist, Color.green, 0.1f);
        }

        // For the 'closest pond in range' diagnostic, walk the ponds
        // once and use Renderer.bounds.ClosestPoint (so the
        // diagnostic reports the geometrically nearest pond even
        // when the player is not currently looking at any of them).
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

        if (!isLookingAtPond && _drawDebug && closestInRange != null)
        {
            if (nearestBlockerDist > 0f && nearestBlockerDist <= _lookDistance)
            {
                Debug.DrawRay(camPos, camForward * nearestBlockerDist, Color.yellow, 0.1f);
            }
        }

        if (Time.time - _lastStatusLogTime >= _statusLogInterval)
        {
            _lastStatusLogTime = Time.time;
            // Absolute nearest pond by geometric (closest-point) distance,
            // for the 'how far am I from any pond' diagnostic.
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
            string blockerInfo = nearestBlockerDist > 0f ? nearestBlockerDist.ToString("F2") + "m" : "none";

            if (isLookingAtPond)
            {
                string pondName = nearestPond != null ? nearestPond.name : "null";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " LOOKING at " + pondName +
                    " rayDist=" + nearestDist.ToString("F2") + "m timer=" + _lookTimer.ToString("F2") + "/" + _lookDuration.ToString("F2") +
                    "s | player=" + camPos.ToString("F1") +
                    " absNearest=" + absName + " absDist=" + absDist);
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
