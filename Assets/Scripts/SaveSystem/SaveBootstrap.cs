using UnityEngine;

/// <summary>
/// Drop this on a GameObject in GameScene. On Start it calls
/// <see cref="GamePersistence.LoadIntoGame"/> with the saved blob
/// (if one exists) so every Inject-aware system in the scene picks
/// up its persisted state.
///
/// Without this bootstrap the load path would have to live somewhere
/// ad-hoc (a random MonoBehaviour's Start), which is fragile and
/// order-dependent. Having a dedicated component makes the load
/// ordering predictable and easy to disable for debugging.
///
/// New Game flow: clear the PlayerPrefs key (SaveSystem.DeleteSave)
/// before scene load, then drop this in the scene as usual — HasSave
/// will be false and the call is a no-op.
///
/// Position is applied AFTER LoadIntoGame so the player teleport
/// doesn't fight cell initialisation, and after a single frame so
/// the CharacterController / Cinemachine have time to wake up.
/// </summary>
public class SaveBootstrap : MonoBehaviour
{
    [Tooltip("If true, runs LoadIntoGame in Start(). Disable for tests or for hot-reload sessions where you want a fresh run.")]
    [SerializeField] private bool _loadOnStart = true;

    private SaveData _pendingApply;

    private void Start()
    {
        if (!_loadOnStart) return;

        if (!SaveSystem.HasSave()) return;

        SaveData data = SaveSystem.Load();
        if (data == null) return;

        // Apply money/hero/fog/map/dialogs/inventory to whatever systems
        // are already alive in the scene by the time we reach Start.
        GamePersistence.LoadIntoGame(data);

        // Position needs the player to exist and Awake/Start to have run
        // for the CharacterController / camera. Defer one frame.
        _pendingApply = data;
    }

    private void LateUpdate()
    {
        if (_pendingApply == null) return;
        var player = PlayerMovement.Instance;
        if (player != null && _pendingApply.position != null)
        {
            player.transform.position = new Vector3(
                _pendingApply.position.x,
                _pendingApply.position.y,
                _pendingApply.position.z);
        }
        _pendingApply = null;
    }
}
