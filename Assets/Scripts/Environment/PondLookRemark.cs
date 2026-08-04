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
    [Tooltip("If true, the component casts a Physics.Raycast and treats any " +
        "TerrainCollider hit (only terrain, not walls/items/trees) as a blocker. " +
        "This is the canonical Unity way to do 'I cannot see the pond through " +
        "the ground' line-of-sight for a terrain-based world. If false, the " +
        "component falls back to v13's pure bounds check (no blockers at all).")]
    [SerializeField] private bool _useTerrainBlocker = true;

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
        // Round 88 v15: blocker check is now type-based (TerrainCollider
        // only) instead of layer-based. v14 used _blockerMask to
        // select the terrain layer, but the user reported that when
        // the terrain was moved to a different layer the blocker
        // check stopped working ('Если я перемещаю terrain на другой
        // layer, то камера начинает его игнорировать'). The root
        // cause is that the user-configured _blockerMask was either
        // missing the terrain's actual layer (so the raycast was
        // checking a layer that had no terrain on it) or the
        // terrain was on a layer that the user had not ticked in
        // the mask. The type-based check ('is the hit collider a
        // TerrainCollider?') does not depend on the layer at all -
        // it uses the C# type system, which is independent of the
        // Inspector configuration. The user just needs to flip
        // _useTerrainBlocker on/off in the Inspector, no layer
        // configuration required.
        //
        // Why RaycastNonAlloc and not single Raycast: the user
        // asked 'terrain should block, but other things should
        // not'. That means the raycast may hit a non-terrain
        // collider (wall, item, pond) before it hits a terrain
        // collider (e.g. the player is standing in a fenced area
        // and a fence post is on the line to the pond at 2 m,
        // but the terrain that should block is at 4 m). The
        // single Physics.Raycast would return the fence post at
        // 2 m and we would miss the terrain at 4 m. With
        // RaycastNonAlloc we collect every hit, filter out the
        // non-terrain ones, and pick the nearest remaining
        // terrain hit. That way the terrain-blocks check is
        // independent of what other colliders the ray happens to
        // cross first.
        //
        // Note: TerrainCollider is a C# class derived from
        // Collider, so 'c is TerrainCollider' is the standard
        // type-check. Physics.Raycast does work against
        // TerrainCollider (the Terrain system has its own
        // internal raycasting path that does not go through
        // the non-convex MeshCollider limitation, so the
        // round 88 v2 'raycast does not hit non-convex
        // MeshCollider' caveat that hit the pond's *Surface
        // MeshColliders does NOT apply to TerrainCollider).
        float nearestBlockerDist = -1f;
        GameObject nearestBlocker = null;
        if (_useTerrainBlocker)
        {
            int hitCount = Physics.RaycastNonAlloc(ray, _hitsBuffer, _lookDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _hitsBuffer[i].collider;
                if (c == null) continue;
                if (c.name.Contains("Pond")) continue;
                if (!(c is TerrainCollider)) continue;
                float d = _hitsBuffer[i].distance;
                if (nearestBlockerDist < 0f || d < nearestBlockerDist)
                {
                    nearestBlockerDist = d;
                    nearestBlocker = c.gameObject;
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
