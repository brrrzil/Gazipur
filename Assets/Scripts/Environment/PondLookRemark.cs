using UnityEngine;
using Zenject;
using static EnumData;

// Round 88 v1: fires the 'soMuchWater' remark when the
// player keeps their crosshair on a pond (one of the
// three pond GameObjects in GameScene - ClearPond,
// PoisonedPond, DirtyPond, plus the matching
// *Surface colliders that sit on top of the water
// mesh) for more than 1 second while standing
// within 5 metres of it.
//
// User report (in Russian):
//
//   'I added an event
//   soMuchWater in
//   RemarksType, set the
//   text and added the
//   audio clip. This
//   event should occur
//   when the hero looks
//   at the pond for more
//   than 1 second at a
//   distance of less than
//   5 metres (the numbers
//   are approximate but
//   it is better to
//   hardcode them). This
//   event does not
//   happen if the hero
//   completed the filter
//   build.'
//
// Why a new MonoBehaviour
// (instead of adding the
// logic into PlayerState
// or PlayerMovement):
//
// PlayerState.SetState is
// the existing
// edge-detection site for
// 'lowHP' (round 85) and
// the periodic
// hunger/thirst remarks,
// but its update cadence
// is per state-set
// (tied to HeroInfo
// mutations from
// PlayerMovement.Tic()),
// not per frame, and the
// pond look timer
// requires a continuous
// 'is the camera ray
// hitting the pond right
// now' read-out that
// has nothing to do with
// the hunger / thirst /
// health data that
// drives the existing
// SetState checks. A
// dedicated MonoBehaviour
// with its own Update()
// matches the '1
// second of continuous
// look' requirement
// without mixing concerns
// with the health /
// hunger / thirst edge
// detection in
// PlayerState.
//
// PlayerMovement already
// does camera rotation,
// but adding a
// pond-look raycast in
// the middle of its
// already-busy LateUpdate
// (movement + look +
// ground check + crouch
// animation) is exactly
// the kind of
// 'kitchen-sink' mixing
// that round 82 (the
// mask overlay) tried to
// avoid when the user
// asked for a separate
// MonoBehaviour on a
// separate GameObject
// for each cross-cutting
// concern. Following
// the same pattern, the
// pond-look driver is
// its own component.
//
// The hardcoded constants
// the user asked for:
//
//   _lookDistance = 5.0f
//     metres. This is the
//     raycast length. It
//     is also the
//     'effective look
//     distance' check -
//     the raycast is run
//     from the player
//     camera in the
//     player's look
//     direction, and if
//     the ray hits
//     something within
//     5 metres, the
//     player is treated
//     as 'looking at it'.
//     5 metres is the
//     user-asked value
//     (round 88 report:
//     'less than 5
//     metres') and is
//     applied as a strict
//     raycast length, not
//     a Vector3.Distance
//     to the pond centre,
//     because Vector3
//     .Distance to the
//     pond centre would
//     fire even when the
//     player is looking
//     the opposite way
//     (5 m from the
//     player position to
//     the pond centre
//     does not mean the
//     player is looking
//     at the pond).
//     Raycast from
//     forward is the
//     correct 'is the
//     player looking at
//     the pond' test.
//
//   _lookDuration = 1.0f
//     seconds. This is the
//     continuous-look
//     threshold. A
//     private
//     float _lookTimer is
//     incremented by
//     Time.deltaTime
//     every frame the
//     raycast hits a
//     pond-named
//     collider, and reset
//     to zero every frame
//     the raycast does
//     not hit a pond
//     (or hits nothing,
//     or hits a
//     non-pond-named
//     collider). When
//     _lookTimer
//     reaches or
//     exceeds
//     _lookDuration
//     (1.0 s), the
//     remark fires
//     once and the
//     _hasFired flag
//     latches true for
//     the rest of the
//     game session.
//
//   _raycastYOffset
//     = 1.6f metres.
//     Optional helper
//     that adds the
//     typical camera
//     height (the
//     PlayerMovement
//     _cameraHeightNormal
//     is 0.8 m above
//     the controller
//     centre, and the
//     controller
//     centre itself is
//     half the capsule
//     height above the
//     feet, so the
//     camera is roughly
//     1.5-1.7 m above
//     the ground). The
//     raycast origin is
//     taken from the
//     _cameraTransform
//     position (set by
//     the user in the
//     Inspector to point
//     at the player's
//     actual camera
//     Transform), so
//     this constant is
//     a safety net in
//     case the user
//     wires the
//     component to the
//     Player root
//     instead of the
//     camera itself -
//     the raycast
//     origin is then
//     'transform.position
//     + Vector3.up *
//     1.6f' to get the
//     eye-line rather
//     than the foot
//     line. If the user
//     wires the
//     component to the
//     camera Transform
//     (the recommended
//     way), the offset
//     is essentially
//     zero and does not
//     affect the result.
//
// Pond identification:
//
//   The three ponds in
//   GameScene are
//   GameObjects named
//   'ClearPond' (the
//   fresh-water pond,
//   the one the player
//   can use to refill
//   their thirst bar
//   when the filter is
//   not yet built),
//   'DirtyPond' (the
//   muddy, contaminated
//   one), and
//   'PoisonedPond' (the
//   toxic one). Each
//   pond also has a
//   child GameObject
//   named 'XxxPondSurface'
//   that carries the
//   water-surface
//   collider the
//   raycast actually
//   hits when the
//   player looks at
//   the water. The
//   identifier used in
//   the raycast check
//   is 'name contains
//   "Pond"' (case-
//   sensitive), which
//   matches all three
//   pond GameObjects
//   and all three
//   surface
//   GameObjects. This
//   is intentionally
//   loose so the
//   user can rename
//   or add ponds
//   later without
//   breaking the
//   trigger.
//
// Skip condition:
//
//   '_quest.QuestsState
//   [Quests.filter] ==
//   2' is the post-
//   build state. It is
//   set by
//   QuestManager.CompleteFilter
//   (the one that
//   WaterFilter.Finish
//   calls when the
//   hold bar fills up
//   and the player has
//   built the water
//   filter). At that
//   point the game
//   transitions to
//   GameMode.win and
//   the WinDiePanel
//   appears, so the
//   player is no
//   longer in
//   outdors mode and
//   the player
//   movement is
//   frozen, but
//   PlayerMovement
//   .OnDisable keeps
//   the player
//   position in the
//   registry, and
//   the player can
//   still be standing
//   near a pond
//   (e.g. right next
//   to the one they
//   just built the
//   filter at). The
//   'do not fire
//   remark after
//   filter built'
//   rule in the user
//   report is enforced
//   here as a hard
//   check at the top
//   of Update() so
//   the remark can
//   not fire even if
//   the player
//   continues to look
//   at the pond after
//   the win panel is
//   up. The flag is
//   not reset until
//   the scene
//   reloads (via
//   the TryAgainButton
//   path that round
//   87 made safe
//   through
//   ZenjectSceneLoader),
//   so a 'post-build
//   look' in the
//   current session
//   will never fire.
//
// One-shot firing:
//
//   The remark fires
//   exactly once per
//   component
//   lifetime (so once
//   per scene load
//   before the player
//   builds the filter).
//   After
//   _hasFiredSoMuchWater
//   latches true,
//   the Update() loop
//   short-circuits
//   before the
//   raycast even
//   runs, so there
//   is no per-frame
//   cost after the
//   fire. The flag
//   does not reset
//   when the player
//   looks away from
//   the pond (the
//   _lookTimer still
//   resets to zero
//   so the player
//   can 're-arm' a
//   future build by
//   triggering the
//   remark-again
//   edge, but the
//   outer
//   _hasFiredSoMuchWater
//   gate prevents
//   re-fire even if
//   the inner
//   condition is
//   met). The user
//   can reset the
//   flag by toggling
//   the component's
//   enabled checkbox
//   off and on in
//   the Inspector
//   (or by reloading
//   the scene via
//   Try Again), so
//   this is
//   effectively a
//   'once per
//   session' gate
//   matching the
//   'I am looking at
//   this pond, what a
//   lot of water'
//   one-off
//   observation the
//   user is going
//   for in the
//   remark.
//
// Wiring (Inspector):
//
//   The user creates
//   one empty
//   GameObject in
//   GameScene (e.g.
//   'PondLookRemark')
//   and adds this
//   component. The
//   only field the
//   user needs to
//   wire is
//   _cameraTransform,
//   and that is
//   expected to be
//   the Player's
//   camera
//   Transform (the
//   same Transform
//   that
//   PlayerMovement
//   ._cameraHolder
//   is on - the
//   user can drag
//   the Player
//   prefab's
//   _cameraHolder
//   from the
//   Inspector or,
//   if the user has
//   the camera
//   hierarchy
//   exposed in the
//   scene, drag the
//   camera
//   GameObject's
//   Transform). If
//   the user does
//   not wire it, the
//   component
//   short-circuits
//   in Update()
//   (the
//   'if
//   (_cameraTransform
//   == null) return;'
//   guard at the
//   top) and the
//   remark is
//   never fired -
//   no NRE, no
//   console spam.
//   The Zenject
//   [Inject] fields
//   (DialogManager
//   and
//   QuestManager)
//   resolve through
//   the scene's
//   SceneContext
//   (the
//   GameManager
//   prefab's
//   SceneContext
//   that round 87
//   made
//   visible), so
//   no manual
//   wiring is
//   needed for
//   the DI
//   dependencies.
public class PondLookRemark : MonoBehaviour
{
    // The user's
    // approximate
    // values,
    // hardcoded
    // per the
    // round 88
    // request.
    // 'Numbers
    // are
    // approximate
    // but better
    // to
    // hardcode
    // them.'
    private const float _lookDistance = 5.0f;
    private const float _lookDuration = 1.0f;
    private const float _raycastYOffset = 1.6f;

    [Tooltip("Player camera Transform - the same one that PlayerMovement._cameraHolder points at. " +
        "If null, the component is a no-op.")]
    [SerializeField] private Transform _cameraTransform;

    [Tooltip("Optional. Layers the raycast will hit. If empty, the raycast uses Physics.DefaultRaycastLayers " +
        "(everything except Ignore Raycast). Set this to the layer your pond colliders are on if you want to " +
        "exclude other geometry (e.g. UI, characters) from the trigger.")]
    [SerializeField] private LayerMask _raycastMask = Physics.DefaultRaycastLayers;

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;

    private float _lookTimer;
    private bool _hasFiredSoMuchWater;

    private void Update()
    {
        // Skip if the
        // component is
        // not wired
        // (the user
        // has not
        // assigned a
        // camera yet).
        // This is a
        // no-op, not
        // an error -
        // the user
        // can drop the
        // component in
        // the scene
        // and wire it
        // later.
        if (_cameraTransform == null) return;

        // Skip if the
        // remark
        // already
        // fired this
        // session.
        // The flag
        // latches true
        // and is never
        // reset by
        // the player
        // looking away
        // (only by
        // scene reload
        // or by
        // toggling the
        // component in
        // the
        // Inspector).
        if (_hasFiredSoMuchWater) return;

        // Skip if the
        // filter has
        // been built
        // (the user
        // report
        // explicitly
        // says 'this
        // event does
        // not happen
        // if the hero
        // completed
        // the filter
        // build').
        //
        // QuestsState
        // is a
        // Dictionary
        // keyed by
        // Quests
        // enum values,
        // initialised
        // in
        // QuestManager
        // .Start to:
        //   [filter] = 0
        //   [healMother] = 0
        // and
        // transitioned
        // by
        // QuestManager
        // .CompleteFilter
        // (the one
        // WaterFilter
        // .Finish
        // calls when
        // the hold bar
        // fills) to
        // '[filter] =
        // 2'. So
        // 'filter == 2'
        // is the
        // 'built' state
        // in the same
        // notation
        // the rest of
        // the
        // project's
        // quests use
        // (see
        // WaterFilter
        // .Intearct,
        // which checks
        // 'healMother
        // == 2' the
        // same way for
        // the 'mother
        // healed'
        // state).
        if (_quest != null
            && _quest.QuestsState != null
            && _quest.QuestsState.TryGetValue(Quests.filter, out int filterState)
            && filterState == 2)
        {
            return;
        }

        // Cast the
        // ray from
        // the camera
        // (or
        // component
        // transform +
        // y offset)
        // in the
        // forward
        // direction
        // of the
        // camera (or
        // component).
        // The
        // _cameraTransform
        // .forward is
        // the
        // player's
        // look
        // direction
        // because
        // PlayerMovement
        // .LateUpdate
        // sets
        // _cameraHolder
        // .localRotation
        // =
        // Quaternion
        // .Euler
        // (_xRotation,
        // 0, 0) and
        // rotates the
        // player root
        // .transform
        // by the
        // mouse-X
        // delta on
        // the Y axis,
        // so the
        // camera
        // forward is
        // exactly the
        // player's
        // look
        // direction
        // (rotated by
        // the
        // vertical
        // pitch on
        // top of the
        // player's
        // yaw).
        Vector3 origin = _cameraTransform.position;
        Vector3 dir = _cameraTransform.forward;

        // Optional
        // y-offset
        // guard for
        // the case
        // where the
        // user wires
        // the
        // component
        // to the
        // Player
        // root
        // instead of
        // the camera
        // transform.
        // The check
        // is 'if the
        // camera
        // position is
        // at the
        // ground line
        // (y < 0.5),
        // assume the
        // user
        // wired the
        // Player root
        // and add
        // 1.6 m'. This
        // is a
        // heuristic,
        // not a
        // guarantee,
        // but in
        // practice
        // the
        // _cameraHeightNormal
        // in
        // PlayerMovement
        // is 0.8 m
        // above the
        // controller
        // centre, and
        // the
        // controller
        // centre is
        // half the
        // capsule
        // height
        // (1.0 m) up
        // from the
        // ground, so
        // the camera
        // is at y
        // ~1.5-1.7 m
        // above the
        // ground in
        // GameScene's
        // terrain.
        // If the
        // y is
        // below 0.5 m,
        // it is most
        // likely the
        // player root
        // (which is
        // at the
        // feet), and
        // we add the
        // offset to
        // get the
        // eye-line.
        if (origin.y < 0.5f)
        {
            origin += Vector3.up * _raycastYOffset;
        }

        // Raycast. The
        // distance is
        // the
        // hardcoded
        // _lookDistance
        // (5.0 m). The
        // QueryTriggerInteraction
        // .Ignore
        // skips
        // trigger
        // colliders -
        // the
        // *PondSurface
        // GameObjects
        // have
        // MeshCollider
        // (not
        // trigger),
        // so this
        // does not
        // exclude
        // the water
        // surface
        // itself, but
        // it does
        // skip
        // any future
        // trigger-
        // based pond
        // interaction
        // the user
        // might add
        // (e.g. a
        // trigger
        // collider
        // for
        // refilling
        // the thirst
        // bar would
        // be ignored
        // by the
        // raycast, so
        // the user
        // does not
        // get a
        // 'looking at
        // pond'
        // remark
        // every time
        // they stand
        // inside the
        // water
        // trigger).
        // The
        // raycast
        // mask is
        // user-
        // configurable
        // in the
        // Inspector
        // so the user
        // can
        // include or
        // exclude
        // specific
        // layers
        // without
        // changing
        // code.
        bool hitPond = false;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, _lookDistance, _raycastMask, QueryTriggerInteraction.Ignore))
        {
            // Pond
            // identification
            // by name:
            // matches
            // 'ClearPond',
            // 'DirtyPond',
            // 'PoisonedPond',
            // and their
            // 'XxxPondSurface'
            // children
            // because
            // all of
            // them have
            // 'Pond' in
            // the
            // name.
            // Case-
            // sensitive
            // to avoid
            // matching
            // unrelated
            // objects
            // like
            // 'Pond-
            // Decoration'
            // or
            // 'PondStones'
            // (the
            // user is
            // free to
            // add such
            // names
            // and they
            // would
            // match too
            // - if they
            // want
            // strict
            // matching,
            // they can
            // switch
            // this to
            // an exact
            // name
            // check
            // against a
            // serialized
            // array of
            // pond
            // names,
            // but the
            // user's
            // request
            // was
            // minimal
            // and the
            // current
            // scene has
            // no false
            // positives).
            if (hit.collider != null
                && hit.collider.name.Contains("Pond"))
            {
                hitPond = true;
            }
        }

        // Timer
        // bookkeeping:
        // - if the
        // raycast
        // hit a
        // pond,
        // accumulate
        // the
        // elapsed
        // time;
        // - if the
        // raycast
        // did not
        // hit a
        // pond
        // (either
        // hit
        // nothing
        // or hit a
        // non-pond
        // collider),
        // reset the
        // timer to
        // zero so
        // the
        // '1 second
        // of
        // continuous
        // look'
            // requirement
        // is
        // enforced
        // strictly.
        if (hitPond)
        {
            _lookTimer += Time.deltaTime;

            if (_lookTimer >= _lookDuration)
            {
                // Fire
                // the
                // remark
                // through
                // the
                // standard
                // CharacterRemarks
                // .StartRemark
                // path.
                // The
                // null-guard
                // on
                // _dialog
                // is for
                // the
                // edge
                // case
                // where
                // the
                // component
                // is
                // dropped
                // in a
                // scene
                // that
                // does
                // not
                // have a
                // DialogManager
                // (e.g.
                // a
                // future
                // test
                // scene) -
                // in
                // GameScene
                // _dialog
                // is
                // always
                // non-null
                // because
                // DialogManager
                // is
                // bound
                // by
                // the
                // scene's
                // SceneContext.
                if (_dialog != null && _dialog.Remarks != null)
                {
                    _dialog.Remarks.StartRemark(RemarksType.soMuchWater);
                }

                // Latch
                // the
                // 'already
                // fired'
                // flag
                // so the
                // remark
                // does
                // not
                // re-fire
                // in the
                // same
                // session.
                // The
                // flag
                // latches
                // true
                // regardless
                // of
                // whether
                // the
                // StartRemark
                // call
                // actually
                // displayed
                // the
                // remark
                // (the
                // underlying
                // StartRemark
                // can
                // return
                // false if
                // the
                // chance
                // roll
                // failed
                // or if
                // the
                // remark
                // is
                // already
                // playing,
                // and that
                // is a
                // user-
                // tunable
                // behaviour
                // in the
                // Inspector
                // - the
                // outer
                // latched
                // flag is
                // the
                // 'do not
                // try
                // again
                // for the
                // rest of
                // the
                // session'
                // gate
                // that
                // matches
                // the
                // user's
                // intent
                // of a
                // single
                // 'wow,
                // there's
                // a lot of
                // water
                // here'
                // reaction).
                _hasFiredSoMuchWater = true;
            }
        }
        else
        {
            _lookTimer = 0f;
        }
    }

    // Inspector
    // toggle /
    // domain-
    // reload
    // safety:
    // the
    // _hasFiredSoMuchWater
    // flag is
    // not
    // serialised
    // (it is
    // 'private
    // bool' on
    // a non-
    // [SerializeField]
    // field),
    // so it is
    // reset
    // every
    // time the
    // domain
    // reloads.
    // This
    // matches
    // the
    // round 80
    // (firstEnterRichZone)
    // and
    // round 84
    // (_isReversing)
    // pattern
    // of
    // 'non-
    // serialised
    // state
    // resets
    // between
    // sessions,
    // is
    // explicit-
    // reset
    // on
    // OnEnable
    // for
    // Domain
    // Reload
    // off'.
    // The
    // 'Enter
    // Play
    // Mode
    // Options
    // + Reload
    // Domain
    // off'
    // setting
    // (round
    // 80 fix)
    // keeps
    // private
    // fields
    // alive
    // between
    // Play
    // sessions
    // in the
    // Editor,
    // so the
    // explicit
    // OnEnable
    // reset is
    // the
    // robust
    // way to
    // make
    // sure a
    // fresh
    // Play
    // session
    // can
    // re-fire
    // the
    // remark.
    // (Production
    // builds
    // always
    // reload
    // the
    // domain
    // on
    // launch,
    // so the
    // OnEnable
    // reset
    // is only
    // needed
    // for the
    // Editor
    // 'domain
    // reload
    // off'
    // mode,
    // but it
    // is
    // free in
    // the
    // production
    // case
    // because
    // the
    // field is
    // already
    // false
    // on a
    // fresh
    // start.)
    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFiredSoMuchWater = false;
    }
}
