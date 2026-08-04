using UnityEngine;
using Zenject;
using static EnumData;

// Fires the 'soMuchWater' remark when the player keeps their
// crosshair on a pond for more than 1 second while within 25
// metres of it. Skips the remark after the filter has been built
// (QuestsState[Quests.filter] == 2).
//
// The component is placed directly on the pond GameObject (the one
// that has the MeshRenderer). It uses its own Renderer's AABB
// for the visibility check, so no external pond reference needs to
// be wired in the Inspector. Camera.main is used for the raycast
// (centre-of-screen = crosshair direction). Only TerrainCollider
// hits block the visibility - walls, fences, trees and items do
// not.
[RequireComponent(typeof(Renderer))]
public class PondLookRemark : MonoBehaviour
{
    private const float _lookDistance = 25.0f;
    private const float _lookDuration = 1.0f;

    [Inject] private DialogManager _dialog;
    [Inject] private QuestManager _quest;

    private float _lookTimer;
    private bool _hasFiredSoMuchWater;
    private bool _initialised;
    private Renderer _renderer;
    private Camera _camera;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _camera = Camera.main;
        _initialised = _renderer != null
            && _camera != null
            && _dialog != null
            && _quest != null
            && _dialog.Remarks != null;
    }

    private void Update()
    {
        if (!_initialised) return;
        if (_hasFiredSoMuchWater) return;

        if (_quest.QuestsState.TryGetValue(Quests.filter, out int filterState) && filterState == 2) return;

        Ray ray = _camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        if (!_renderer.bounds.IntersectRay(ray, out float dist) || dist > _lookDistance)
        {
            _lookTimer = 0f;
            return;
        }

        // Terrain-only blocker check: if the ray hits a terrain
        // collider between the camera and the pond, the pond is
        // not visible (the player is looking at the ground). Walls,
        // fences, trees, items, the Player capsule itself - all of
        // those are ignored, the player can 'see' the pond through
        // them.
        if (Physics.Raycast(ray, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider is TerrainCollider)
        {
            _lookTimer = 0f;
            return;
        }

        _lookTimer += Time.deltaTime;
        if (_lookTimer >= _lookDuration)
        {
            _dialog.Remarks.StartRemark(RemarksType.soMuchWater);
            _hasFiredSoMuchWater = true;
        }
    }

    private void OnEnable()
    {
        _lookTimer = 0f;
        _hasFiredSoMuchWater = false;
    }
}
