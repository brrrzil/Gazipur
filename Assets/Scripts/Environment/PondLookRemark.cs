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

        // Round 88 v13: drop Physics.Raycast entirely, use only
        // Renderer.bounds.IntersectRay per pond. The user's
        // feedback 'Пусть он реагирует только на MeshRenderer'
        // (the ray should react only to MeshRenderer) is
        // explicit - they want a pure visual / bounds-based
        // detection that does not care about any colliders at
        // all (not the pond's MeshCollider, not the terrain
        // MeshCollider, not the wall BoxCollider, nothing). The
        // check is: for each pond, does the camera ray pierce
        // the pond's MeshRenderer world-space AABB within 5
        // metres? If yes, the player is looking at the pond and
        // the remark can start ticking.
        //
        // Why this is the right abstraction here:
        //   - MeshRenderer.bounds.IntersectRay works on the
        //     renderer's AABB, which is always available on any
        //     GameObject with a MeshRenderer regardless of
        //     collider type, convexity, trigger / non-trigger,
        //     etc. It is the same AABB the player's eyes use to
        //     see the mesh (the renderer is what draws the mesh
        //     on screen, the AABB is its world-space bounding
        //     box).
        //   - The previous v10 / v11 / v12 attempts at using
        //     Physics.Raycast all ran into the same issue: the
        //     pond's MeshCollider (and / or other scene
        //     colliders the user did not want to count as
        //     blockers) showed up as hits in the raycast, and
        //     either blocked the ray prematurely or were
        //     classified as 'pond hits' that still consumed
        //     the raycast slot. With v13 there is no raycast at
        //     all - the question 'is the player looking at a
        //     pond' is answered purely by 'does the camera ray
        //     pierce the pond's AABB', which is the same
        //     question the user's eyes answer when they look at
        //     the pond on screen.
        //   - Other-geometry blocking (the user's v10 request
        //     'учитывать другие объекты. По условию их не должно
        //     быть между взглядом и прудом') is now dropped by
        //     user request in v13. The user has decided that
        //     for this particular remark, 'looking at the pond'
        //     means 'the camera ray hits the pond's AABB',
        //     regardless of whether a wall or a tree is in
        //     between. That is a clean, simple definition that
        //     matches the player's perception of 'I am looking
        //     at the pond' (the player's gaze pierces walls in
        //     the sense that they know what they are aiming at,
        //     and the game does not need to second-guess them
        //     with a line-of-sight test for this particular
        //     remark). If the user later decides they want
        //     line-of-sight blocking, v13's logic can be
        //     extended with a Physics.Raycast + blocker check
        //     again, but for now the pure-bounds check is what
        //     the user asked for.
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

            if (isLookingAtPond)
            {
                string pondName = nearestPond != null ? nearestPond.name : "null";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " LOOKING at " + pondName +
                    " boundsDist=" + nearestDist.ToString("F2") + "m timer=" + _lookTimer.ToString("F2") + "/" + _lookDuration.ToString("F2") +
                    "s | player=" + camPos.ToString("F1") +
                    " absNearest=" + absName + " absDist=" + absDist);
            }
            else
            {
                string nearestName = closestInRange != null ? closestInRange.name : "<none in 5m>";
                string distStr = closestInRange != null ? closestInRangeDist.ToString("F2") + "m" : "n/a";
                Debug.Log("[PondLookRemark] frame=" + _frameCount + " idle in-5m=" + nearestName +
                    " dist=" + distStr +
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
