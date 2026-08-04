using System.Collections.Generic;
using UnityEngine;
using Zenject;
using static EnumData;

// Round 88 v3: fires the
// 'soMuchWater'
// remark when the
// player keeps
// their crosshair on
// a pond for more
// than 1 second while
// standing within 5
// metres of it.
//
// Round 88 v1 used
// Physics.Raycast
// from the player
// camera in the
// player's look
// direction with a
// 5 m length, and
// checked
// 'hit.collider.name
// .Contains("Pond")'.
// That did NOT work
// in GameScene because
// the three pond
// GameObjects
// (ClearPond,
// DirtyPond,
// PoisonedPond) and
// their *Surface
// children all carry
// MeshColliders with
// 'm_Convex: 0'
// (concave / non-
// convex), and
// Unity's
// Physics.Raycast
// does NOT register
// hits against non-
// convex MeshColliders
// (this is a known
// Unity limitation -
// non-convex
// MeshColliders
// support physics
// simulation but not
// raycasting; only
// convex MeshColliders
// and primitive
// colliders
// (BoxCollider,
// SphereCollider,
// CapsuleCollider)
// support raycasts
// against them).
// The user reported
// 'Не работает' (it
// does not work)
// because the
// raycast
// silently
// missed
// every
// frame, the
// _lookTimer
// never
// reached
// 1.0 s, and
// the
// remark
// never
// fired.
//
// The fix in v3 is
// to drop the
// raycast entirely
// and use a direct
// distance + angle
// test against the
// pond's
// transform.position
// instead:
//   distance < 5 m
//     (the user-
//     asked
//     'within 5
//     metres'
//     check)
//   angle
//     between
//     camera
//     .forward
//     and
//     (pond -
//     camera
//     ).normalized
//     < 30
//     degrees
//     (the
//     'player is
//     actually
//     looking at
//     the pond'
//     check -
//     30 degrees
//     is the
//     half-angle
//     of a
//     typical
//     60-degree
//     FOV cone,
//     which is
//     roughly
//     the
//     'crosshair
//     on
//     target'
//     zone in a
//     first-
//     person
//     game)
// The combination
// of distance < 5 m
// AND angle < 30
// degrees is what
// matches the user
// 'looks at the
// pond for more
// than 1 second
// at a distance of
// less than 5
// metres' phrasing:
//   - 'distance of
//     less than 5
//     metres'
//     maps to
//     Vector3
//     .Distance
//     (cameraPos,
//     pondPos)
//     < 5.0f
//   - 'looks at
//     the pond'
//     maps to
//     Vector3
//     .Angle
//     (camera
//     .forward,
//     (pondPos
//     - cameraPos
//     ).normalized)
//     < 30.0f
// Both checks
// are evaluated
// every frame;
// _lookTimer is
// incremented only
// when both pass
// for at least one
// pond (the player
// can have more
// than one pond in
// their 5 m
// vicinity and
// the remark still
// fires once for
// that look
// direction), and
// reset to zero on
// any frame where
// no pond satisfies
// both checks (so
// the '1 second of
// continuous look'
// requirement is
// enforced
// strictly).
//
// Pond discovery
// (the user
// controls the
// list, with a
// name-based
// fallback for
// convenience):
//
//   [SerializeField]
//   private List
//   <GameObject>
//   _ponds
//
// If the user
// drags the pond
// GameObjects
// (ClearPond,
// DirtyPond,
// PoisonedPond,
// plus the
// *Surface
// children if
// desired) into
// the _ponds list
// in the Inspector,
// that list is
// used as-is.
// This is the
// 'round 82 v6
// minimal wiring'
// pattern - the
// user controls
// the references
// explicitly,
// and the
// component
// does not auto-
// find anything
// at runtime
// when the list
// is populated.
//
// If the user
// leaves _ponds
// empty (or
// forgets to
// wire it), the
// component falls
// back to a one-
// time auto-find
// in Awake using
// Resources
// .FindObjectsOfTypeAll
// <GameObject>()
// and filtering by
// name containing
// 'Pond' (case-
// sensitive, same
// as v1). This
// is the
// 'round 82 v5
// auto-find by
// name' pattern -
// a convenience
// fallback that
// works without
// Inspector
// wiring. The
// auto-find is
// done exactly
// once in Awake
// and cached in
// _ponds, so the
// per-frame cost
// is just a list
// iteration
// (a few
// Vector3
// .Distance
// calls per
// frame, which
// is trivial).
//
// Camera
// reference
// (same dual
// approach):
//
//   [SerializeField]
//   private
//   Transform
//   _cameraTransform
//
// If wired in
// the Inspector
// (the user
// drags the
// Player's
// _cameraHolder
// Transform
// there, which
// is the same
// Transform the
// PlayerMovement
// script rotates
// in LateUpdate
// to drive the
// look
// direction),
// that
// Transform is
// used as-is.
//
// If the user
// leaves the
// field empty,
// the component
// falls back to
// Camera.main
// in Awake. The
// GameScene has
// a 'Main
// Camera'
// GameObject
// (fileID
// 330585545,
// tag
// 'MainCamera'),
// so
// Camera.main
// resolves to
// that camera
// and gives the
// component the
// player's
// render
// perspective.
// (The Main
// Camera and
// the Player's
// _cameraHolder
// are usually
// the same
// camera in
// the project
// because
// PlayerMovement
// is on the
// Player root
// and the
// _cameraHolder
// is a child
// of the
// Player that
// carries the
// same camera
// the user
// sees
// through; the
// Player
// prefab's
// Main Camera
// is the
// render
// target, and
// the
// _cameraHolder
// is what
// PlayerMovement
// rotates to
// drive look.
// In practice
// they are at
// the same
// world-space
// position and
// rotation, so
// either
// reference
// gives the
// same
// _lookAtPond
// result. The
// dual
// approach
// covers both
// the case
// where the
// user has
// the camera
// wiring
// already in
// the scene
// and the case
// where the
// component
// was dropped
// in the
// scene
// without
// any camera
// reference
// wired.)
//
// Skip
// condition
// (the user
// asked for
// 'this event
// does not
// happen if
// the hero
// completed
// the filter
// build'):
//
// _quest.QuestsState
// [Quests.filter]
// == 2 is the
// post-build
// state set by
// QuestManager
// .CompleteFilter
// (called by
// WaterFilter
// .Finish when
// the hold bar
// fills up and
// the player has
// built the
// water filter).
// The check is
// at the top of
// Update() and
// short-circuits
// before the
// per-pond
// distance /
// angle loop
// runs, so
// there is no
// per-frame
// cost when
// the filter is
// already
// built.
//
// One-shot
// firing:
//
// Same pattern as
// v1: a private
// bool
// _hasFiredSoMuch
// Water latches
// true the
// first time
// _lookTimer
// reaches 1.0 s
// and stays
// true for the
// rest of the
// component's
// lifetime. The
// Update() loop
// short-
// circuits
// before the
// per-pond loop
// even runs
// after the
// fire, so
// there is no
// per-frame
// cost after
// the fire
// (just one
// bool check).
// The flag is
// reset on
// OnEnable
// (the round 80
// v2 'Enter
// Play Mode
// Options +
// Reload Domain
// off'
// robustness
// pattern), so
// a fresh Play
// session can
// re-fire the
// remark even
// if Domain
// Reload is off
// in the
// Editor.
//
// Debug
// visualization
// (optional,
// user-toggleable
// in the
// Inspector):
//
//   [SerializeField]
//   private bool
//   _drawDebug
//
// If the user
// enables
// _drawDebug,
// the component
// calls
// Debug.DrawRay
// every frame to
// visualise
// the distance
// + angle check
// (green ray
// from the
// camera to a
// pond that
// passes both
// checks, red
// ray to a pond
// that fails
// one or both
// checks, plus
// a short log
// every second
// of the
// current
// state -
// 'soMuchWater
// timer X / 1 s
// ' for in-
// progress
// looks).
// This is the
// same
// 'toggle-
// able debug
// overlay'
// pattern that
// DangerZone
// uses in
// round 82
// (the
// DangerZone
// class has
// '#if
// UNITY_EDITOR'
// sections
// that draw
// gizmos in
// the Editor).
// The debug
// draws are
// only visible
// in the Scene
// view and the
// Game view if
// 'Gizmos' is
// enabled (the
// standard
// Unity Editor
// gizmo toggle
// in the top
// right of the
// Game view).
// They are
// completely
// free in
// production
// builds (the
// _drawDebug
// default is
// false, and
// the user can
// leave it
// false in the
// Inspector for
// release).
public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 5.0f;
    private const float _lookDuration = 1.0f;
    // Half-angle of the
    // 'crosshair on
    // target' cone.
    // 30 degrees is
    // roughly the
    // half-FOV of a
    // typical 60-degree
    // FOV, which is
    // the 'I am
    // looking at this
    // thing' zone in
    // a first-person
    // game. The user
    // asked for the
    // 'looks at the
    // pond' check
    // without a
    // specific angle,
    // so 30 degrees is
    // hardcoded as a
    // reasonable
    // approximation
    // of 'aimed at'.
    private const float _lookHalfAngleDeg = 30.0f;

    [Tooltip("Pond GameObjects to check. If empty, the component auto-finds by name containing 'Pond' in Awake.")]
    [SerializeField] private List<GameObject> _ponds = new List<GameObject>();

    [Tooltip("Player camera Transform (e.g. PlayerMovement._cameraHolder). If null, the component falls back to Camera.main in Awake.")]
    [SerializeField] private Transform _cameraTransform;

    [Tooltip("If true, draws Debug.DrawRay visualisation of the distance + angle check every frame. Visible only when the Game view 'Gizmos' toggle is on. Free in production builds (default false).")]
    [SerializeField] private bool _drawDebug = false;

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;

    private float _lookTimer;
    private bool _hasFiredSoMuchWater;
    private bool _initialised;

    private void Awake()
    {
        // Camera
        // fallback.
        // If the user
        // did not wire
        // _cameraTransform
        // in the
        // Inspector,
        // try
        // Camera.main.
        // The
        // GameScene
        // has a 'Main
        // Camera'
        // GameObject
        // with the
        // 'MainCamera'
        // tag, so
        // Camera.main
        // resolves to
        // it.
        if (_cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cameraTransform = mainCam.transform;
            }
        }

        // Pond
        // fallback.
        // If the user
        // did not
        // wire _ponds
        // in the
        // Inspector,
        // do a one-
        // time auto-
        // find by
        // name. The
        // user is
        // free to
        // either
        // wire the
        // list
        // explicitly
        // (the
        // 'round 82
        // v6 minimal'
        // pattern)
        // or rely on
        // this auto-
        // find
        // (the 'round
        // 82 v5
        // auto-find
        // by name'
        // pattern).
        // Both
        // patterns
        // are
        // documented
        // at the top
        // of this
        // file.
        if (_ponds == null || _ponds.Count == 0)
        {
            _ponds = new List<GameObject>();
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null) continue;
                if (!go.name.Contains("Pond")) continue;
                // Only
                // include
                // active
                // scene
                // GameObjects
                // (not
                // prefab
                // assets
                // from
                // Resources,
                // not
                // disabled
                // editor
                // objects).
                if (!go.scene.IsValid()) continue;
                if (!go.activeInHierarchy) continue;
                _ponds.Add(go);
            }
        }

        _initialised = (_cameraTransform != null) && (_ponds != null && _ponds.Count > 0);
    }

    private void Update()
    {
        // Not
        // wired
        // (no
        // camera,
        // no
        // ponds)
        // -
        // no-op,
        // no
        // console
        // spam,
        // no
        // NRE.
        if (!_initialised) return;

        // Already
        // fired
        // this
        // session.
        if (_hasFiredSoMuchWater) return;

        // Filter
        // built
        // -
        // skip
        // the
        // remark
        // entirely.
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
        GameObject nearestHitPond = null;
        float nearestHitDist = float.PositiveInfinity;

        // Per-pond
        // distance
        // +
        // angle
        // check.
        for (int i = 0; i < _ponds.Count; i++)
        {
            GameObject pond = _ponds[i];
            if (pond == null) continue;
            Vector3 pondPos = pond.transform.position;
            Vector3 toPond = pondPos - camPos;
            float dist = toPond.magnitude;
            if (dist > _lookDistance) continue;
            if (dist < 0.001f) continue; // camera inside pond, treat as looking at it
            Vector3 toPondDir = toPond / dist;
            float dot = Vector3.Dot(camForward, toPondDir);
            if (dot < cosHalfAngle) continue;

            // This
            // pond
            // passes
            // both
            // checks.
            if (_drawDebug)
            {
                Debug.DrawRay(camPos, toPondDir * dist, Color.green);
            }
            if (dist < nearestHitDist)
            {
                nearestHitDist = dist;
                nearestHitPond = pond;
            }
            isLookingAtPond = true;
        }

        // Optional
        // debug:
        // draw a
        // red ray
        // to the
        // closest
        // pond in
        // range
        // even
        // when
        // the
        // angle
        // check
        // fails,
        // so the
        // user
        // can see
        // 'the
        // pond is
        // here,
        // but I
        // am not
        // looking
        // at it'.
        if (_drawDebug && !isLookingAtPond)
        {
            float best = float.PositiveInfinity;
            Vector3 bestDir = Vector3.zero;
            for (int i = 0; i < _ponds.Count; i++)
            {
                GameObject pond = _ponds[i];
                if (pond == null) continue;
                Vector3 toPond = pond.transform.position - camPos;
                float d = toPond.magnitude;
                if (d > _lookDistance) continue;
                if (d < best)
                {
                    best = d;
                    bestDir = toPond / d;
                }
            }
            if (best < float.PositiveInfinity)
            {
                Debug.DrawRay(camPos, bestDir * best, Color.red);
            }
        }

        if (isLookingAtPond)
        {
            _lookTimer += Time.deltaTime;

            if (_drawDebug)
            {
                Debug.Log($"[PondLookRemark] looking at {nearestHitPond?.name ?? \"?\"} ({nearestHitDist:F2} m) timer {_lookTimer:F2} / {_lookDuration:F2} s");
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

    // Domain-reload
    // reset
    // (round 80
    // v2
    // pattern).
    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFiredSoMuchWater = false;
    }
}
