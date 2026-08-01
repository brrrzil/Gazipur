using UnityEngine;

/// <summary>
/// Round 79: trigger handler for the RichZone GameObject in
/// GameScene (Location - Zones - RichZone). When the player
/// first walks into the trigger, fires a one-time
/// CharacterRemarks.StartRemark(RemarksType
/// .firstEnterRichZone) call so the
/// CharacterRemarks system plays the
/// text + voice remark the user
/// configured on its _remarks[]
/// inspector row.
///
/// Background:
///
/// The RichZone trigger was wired up
/// in GameScene.unity with a
/// MeshCollider (m_IsTrigger: 1)
/// and an AudioSource (bound to the
/// Sound mixer group) but the
/// MonoBehaviour on the
/// RichZone GameObject (this
/// file) was previously a
/// placeholder: an empty Start()
/// and an empty Update(). The
/// trigger volume therefore never
/// fired any code; it just sat in
/// the scene with a collider
/// the player could walk through.
///
/// The user added the
/// firstEnterRichZone value to
/// EnumData.RemarksType in a
/// separate commit (f195b80) and
/// configured the corresponding
/// _remarks[] row on
/// CharacterRemarks (text + voice
/// AudioClip + isOneTime=true).
/// This commit wires the trigger
/// to that existing data row.
///
/// Why FindFirstObjectByType and
/// not [Inject] / inspector
/// reference:
///
/// CharacterRemarks is a
/// MonoBehaviour that lives in
/// the scene but is NOT bound
/// through Zenject (no
/// 'Container.Bind<CharacterRemarks>()
/// .FromInstance(...).AsSingle();'
/// line in any installer in the
/// project). The project's
/// existing per-button audio
/// path (round 77) hit a
/// ZenjectException when
/// ButtonAnimation was [Inject]
/// -ing Sounds for the same
/// reason: the binding the
/// [Inject] was looking for was
/// not actually present in any
/// installer. Adding [Inject] on
/// RichZone would either (a) need
/// a new Container.Bind line in
/// GameInstaller for
/// CharacterRemarks, which would
/// also need the user to drag
/// the CharacterRemarks reference
/// into the GameInstaller
/// inspector, or (b) crash at
/// scene start with a
/// ZenjectException, mirroring
/// the ButtonAnimation issue.
///
/// FindFirstObjectByType is
/// called from OnTriggerEnter,
/// which only fires once per
/// scene load (the
/// CharacterRemarks instance is
/// also a single instance per
/// scene). The first call
/// performs a scene search; if
/// the lookup ever turns out to
/// be slow on a hot path, this
/// can be cached into a static
/// field in a follow-up commit,
/// but the cost is one
/// O(scene-graph) walk on the
/// player's first step into the
/// RichZone, which is once per
/// game session. Not worth
/// optimising before it
/// measurably matters.
///
/// isOneTime protection:
///
/// The CharacterRemarks row that
/// the user added in f195b80
/// has isOneTime=true. Even if
/// this OnTriggerEnter fired
/// twice (e.g. the player walked
/// out and back into the
/// trigger), the remark would
/// not play a second time:
/// CharacterRemarks sets
/// 'chance = 0' on the remark
/// the first time it is played
/// (when isOneTime=true), and
/// StartRemark short-circuits
/// when chance < a random
/// roll. The trigger is also
/// only fired by the player's
/// Collider, not by other
/// colliders, so we do not need
/// an extra one-shot gate here.
/// </summary>
public class RichZone : MonoBehaviour
{
    private CharacterRemarks _remarks;

    private void OnTriggerEnter(Collider other)
    {
        // Only the player should fire the remark. Other
        // colliders (NPCs, dropped items, debris) walking
        // through the trigger volume should not play it.
        // The CharacterController on PLAYER is the
        // project-wide convention for 'this is the
        // player' (PlayerMovement has
        // [RequireComponent(typeof(CharacterController))]
        // and no other MonoBehaviour in the project uses
        // a CharacterController on the player rig).
        if (other == null) return;
        if (other.GetComponentInParent<CharacterController>() == null) return;

        if (_remarks == null)
        {
            // Lazy lookup on first hit. Cached in
            // _remarks so subsequent triggers
            // (e.g. the player walks out and back in
            // before the isOneTime flag prevents the
            // second play) skip the scene search.
            // FindFirstObjectByType is the Unity 6
            // replacement for the now-obsolete
            // FindObjectOfType; it does not allocate
            // and returns the instance the scene
            // already loaded.
            _remarks = FindFirstObjectByType<CharacterRemarks>();
        }

        if (_remarks == null) return;

        _remarks.StartRemark(RemarksType.firstEnterRichZone);
    }
}
