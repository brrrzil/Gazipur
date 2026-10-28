using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using Zenject;
using static EnumData;

/// <summary>
/// Runs a scripted cutscene (Timeline / PlayableDirector) and guarantees
/// the world looks exactly the way it did before the sequence started.
///
/// What it does:
///  1. Snapshots the state of every object listed in _restoreTargets
///     (active state, transform, animator state).
///  2. Switches the game into GameMode.cutscene — the player is locked
///     (no walk / no look) but the clock keeps running so Timeline and
///     DOTween can animate. The cursor is hidden for the duration.
///  3. Plays the Timeline.
///  4. When the Timeline ends, restores the snapshot and switches back
///     to the requested mode, then fires _onFinished.
///
/// Why the explicit snapshot: Timeline restores the state of properties
/// it animated through Animation Tracks, but custom tracks (anything
/// that toggles a GameObject or writes a field from script) are not
/// rolled back automatically. Snapshotting every affected object up
/// front means "exit the cutscene" is a single deterministic operation
/// instead of "hope every track cleaned up after itself".
/// </summary>
public class CutsceneController : MonoBehaviour
{
    [Header("Timeline")]
    [Tooltip("The PlayableDirector that holds the cutscene Timeline asset.")]
    [SerializeField] private PlayableDirector _director;

    [Header("State restore")]
    [Tooltip("Objects whose state is snapshotted before the cutscene and restored after it. Add every GameObject the Timeline moves, shows/hides or re-animates.")]
    [SerializeField] private GameObject[] _restoreTargets;

    [Header("Flow")]
    [Tooltip("Game mode to return to when the cutscene ends.")]
    [SerializeField] private GameMode _modeOnFinish = GameMode.outdors;
    [Tooltip("Invoked after the state has been restored and the mode has been switched back.")]
    [SerializeField] private UnityEvent _onFinished;

    [Inject] private GameModeManager _gameMode;

    // Snapshot of one object's state.
    private class Snapshot
    {
        public GameObject Go;
        public bool ActiveSelf;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;
        public bool AnimatorEnabled;
        public int AnimatorStateHash;
        public float AnimatorNormalizedTime;
    }

    private readonly List<Snapshot> _snapshots = new List<Snapshot>();
    private bool _playing;
    private bool _cursorWasVisible;
    private CursorLockMode _cursorLockState;

    /// <summary>True while a cutscene is playing.</summary>
    public bool IsPlaying => _playing;

    private void Awake()
    {
        if (_director != null) _director.stopped += OnDirectorStopped;
    }

    private void OnDestroy()
    {
        if (_director != null) _director.stopped -= OnDirectorStopped;
    }

    /// <summary>
    /// Start the cutscene. Safe to call twice - the second call is
    /// ignored while a sequence is already running.
    /// </summary>
    public void Play()
    {
        if (_playing) return;
        if (_director == null || _director.playableAsset == null)
        {
            Debug.LogWarning("CutsceneController: no Timeline assigned, nothing to play.");
            return;
        }

        _playing = true;
        CaptureState();

        // Remember the cursor so we can put it back exactly as it was.
        _cursorWasVisible = Cursor.visible;
        _cursorLockState = Cursor.lockState;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_gameMode != null) _gameMode.ChangeMode(GameMode.cutscene);

        _director.time = 0;
        _director.Play();
    }

    /// <summary>
    /// Stop the cutscene early, restore the world and fire _onFinished.
    /// Used by a "skip" button if you want one.
    /// </summary>
    public void Stop()
    {
        if (!_playing) return;
        if (_director != null) _director.Stop();
        // OnDirectorStopped handles the restore; but if the director
        // wasn't playing, stopped won't fire, so finish manually.
        if (_playing) Finish();
    }

    private void OnDirectorStopped(PlayableDirector director)
    {
        Finish();
    }

    private void Finish()
    {
        if (!_playing) return;
        _playing = false;

        RestoreState();

        Cursor.visible = _cursorWasVisible;
        Cursor.lockState = _cursorLockState;

        if (_gameMode != null) _gameMode.ChangeMode(_modeOnFinish);

        _onFinished?.Invoke();
    }

    private void CaptureState()
    {
        _snapshots.Clear();
        if (_restoreTargets == null) return;

        for (int i = 0; i < _restoreTargets.Length; i++)
        {
            var go = _restoreTargets[i];
            if (go == null) continue;

            var snap = new Snapshot
            {
                Go = go,
                ActiveSelf = go.activeSelf,
                Position = go.transform.position,
                Rotation = go.transform.rotation,
                LocalScale = go.transform.localScale,
            };

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                snap.AnimatorEnabled = animator.enabled;
                if (animator.enabled && animator.runtimeAnimatorController != null)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    snap.AnimatorStateHash = info.fullPathHash;
                    snap.AnimatorNormalizedTime = info.normalizedTime;
                }
            }

            _snapshots.Add(snap);
        }
    }

    private void RestoreState()
    {
        for (int i = 0; i < _snapshots.Count; i++)
        {
            var s = _snapshots[i];
            if (s.Go == null) continue;

            // Restore the active state first: a deactivated object
            // can't have its transform or animator touched reliably.
            if (s.Go.activeSelf != s.ActiveSelf) s.Go.SetActive(s.ActiveSelf);

            var t = s.Go.transform;
            t.position = s.Position;
            t.rotation = s.Rotation;
            t.localScale = s.LocalScale;

            var animator = s.Go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (!s.AnimatorEnabled)
                {
                    animator.enabled = false;
                }
                else
                {
                    animator.enabled = true;
                    if (s.AnimatorStateHash != 0 && animator.runtimeAnimatorController != null)
                        animator.Play(s.AnimatorStateHash, 0, s.AnimatorNormalizedTime);
                }
            }
        }
        _snapshots.Clear();
    }
}
