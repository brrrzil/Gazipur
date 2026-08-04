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
    [Tooltip("Layer mask for colliders that count as 'blockers' for the look-at-pond check. " +
        "Defaults to Physics.DefaultRaycastLayers. The pond's own colliders are always skipped " +
        "regardless of this mask, and common scene names like 'Terrain' / 'Ground' are skipped too.")]
    [SerializeField] private LayerMask _blockerMask = Physics.DefaultRaycastLayers;

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

        // Cast along the camera forward through the centre of the
        // screen (crosshair direction). The user-wired _blockerMask
        // lets the user narrow what counts as a blocker, default
        // is everything except Ignore Raycast.
        int hitCount = Physics.RaycastNonAlloc(ray, _hitsBuffer, _lookDistance, _blockerMask, QueryTriggerInteraction.Ignore);

        // Build a short log of every non-pond hit for the diagnostic
        // so the user can see exactly what colliders the ray crossed.
        // Throttled to once per second via the existing _statusLogInterval.
        System.Text.StringBuilder hitLog = null;
        if (Time.time - _lastStatusLogTime >= _statusLogInterval)
        {
            hitLog = new System.Text.StringBuilder();
        }

        float nearestPondHitDist = -1f;
        GameObject nearestPondHit = null;
        float nearestBlockerDist = -1f;
        GameObject nearestBlocker = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _hitsBuffer[i].collider;
            if (c == null) continue;
            float d = _hitsBuffer[i].distance;

            // Round 88 v12: skip pond colliders entirely. The user's
            // report was 'невидимые коллайдеры пруда. Поэтому луч не
            // пробивается через них' - the pond's MeshColliders were
            // being treated as blockers and the raycast was terminating
            // at the pond's surface. We filter the pond's own colliders
            // out so the ray 'punches through' the pond geometry. The
            // pond visibility check uses Bounds.IntersectRay below
            // (which always works regardless of collider type).
            if (c.name.Contains("Pond")) continue;

            // Also skip common always-present scene objects that are
            // not meaningful 'I am looking at the pond' blockers.
            if (c.name == "Terrain" || c.name == "Ground") continue;

            if (hitLog != null)
            {
                if (hitLog.Length > 0) hitLog.Append(" | ");
                hitLog.Append(c.name).Append("@").Append(d.ToString("F2"));
            }

            if (nearestBlockerDist < 0f || d < nearestBlockerDist)
            {
                nearestBlockerDist = d;
                nearestBlocker = c.gameObject;
            }
        }

        // The ponds in GameScene have non-convex MeshColliders, and
        // we now skip them entirely above. Visibility is computed via
        // Bounds.IntersectRay for each pond, which works regardless
        // of collider type.
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
            Debug.DrawRay(camPos, rayDir * nearestDist, Color.green, 0.1f);
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

        if (!isLookingAtPond && _drawDebug && nearestBlockerDist > 0f && nearestBlockerDist <= _lookDistance)
        {
            Debug.DrawRay(camPos, rayDir * nearestBlockerDist, Color.yellow, 0.1f);
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
            string hitsInfo = hitLog != null && hitLog.Length > 0 ? hitLog.ToString() : "no hits";

            if (isLookingAtPond)
            {
                string pondName = nearestPond != null ? nearestPond.name : "null";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " LOOKING at " + pondName +
                    " rayDist=" + nearestDist.ToString("F2") + "m timer=" + _lookTimer.ToString("F2") + "/" + _lookDuration.ToString("F2") +
                    "s | hits=[" + hitsInfo + "]" +
                    " | player=" + camPos.ToString("F1") +
                    " absNearest=" + absName + " absDist=" + absDist);
            }
            else
            {
                string nearestName = closestInRange != null ? closestInRange.name : "<none in 5m>";
                string distStr = closestInRange != null ? closestInRangeDist.ToString("F2") + "m" : "n/a";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " idle in-5m=" + nearestName +
                    " dist=" + distStr + " blockerAt=" + blockerInfo +
                    " hits=[" + hitsInfo + "]" +
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
