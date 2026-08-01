using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Round 76: static, lazy-initialised UI audio helper.
///
/// Background:
///
/// The project's central Sounds service and the
/// GameInstaller _sounds binding are not wired in this
/// project right now (GameInstaller._sounds is null in
/// the saved prefab, SoundManager.prefab._uiSound is [],
/// SoundManager.prefab is not instantiated in either
/// scene). So the round-72 [Inject] Sounds path was
/// dead, and the round-74 PlayClipAtPoint fallback
/// created a fresh throwaway GameObject per hover and
/// did not route through the project's AudioMixer.
///
/// Round 75 tried to fix that with a MonoBehaviour
/// UIAudio that finds the EventSystem and adds an
/// AudioSource to it. That class was correct in
/// principle but required a UIAudio component to be on
/// at least one GameObject in the scene for Awake to
/// run. The user did not add it (and was not asked to
/// in round 75's commit), so Awake never fired, the
/// static Instance was never assigned, and the hover
/// remained silent.
///
/// This rewrite turns UIAudio into a static helper
/// with lazy initialisation. The first call to
/// UIAudio.Play(clip) creates a single, persistent
/// GameObject named 'UIAudio', parents the AudioSource
/// on it, and routes the source through the AudioMixer's
/// 'Sound' group. Every subsequent Play call reuses the
/// same source. No MonoBehaviour, no scene-side
/// component, no inspector wiring - it just works the
/// first time the user hovers a button.
///
/// DOTween warning caveat:
///
/// The user's Unity console also shows:
///
///   'Some objects were not cleaned up when closing
///    the scene. (Did you spawn new GameObjects
///    from OnDestroy?) ... [DOTween]'
///
/// That warning is from the DOTween library
/// (using DG.Tweening in ButtonAnimation's
/// DORotate calls). DOTween spawns its own internal
/// GameObject for the TweenManager the first time
/// any DORotate / DO* method runs, and Unity's play-
/// mode shutdown sometimes catches DOTween's cleanup
/// after Unity has already started tearing the scene
/// down. It is not caused by UIAudio and not caused
/// by this commit; it has been present since the
/// rotate-on-hover animation was added in earlier
/// rounds.
/// </summary>
public static class UIAudio
{
    private const string MixerResourceName = "AudioMixer";
    private const string MixerGroupName = "Sound";
    private const string SourceGameObjectName = "UIAudio";

    private static AudioSource _source;

    /// <summary>
    /// Play a one-shot UI clip through the central,
    /// mixer-routed AudioSource. Creates the source
    /// on the first call; reuses it on every later
    /// call. Null-safe on the clip. Multiple rapid
    /// calls overlap correctly because PlayOneShot
    /// does not truncate the previous play.
    /// </summary>
    public static void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (_source == null) Init();
        if (_source == null) return; // Init failed
        _source.PlayOneShot(clip);
    }

    private static void Init()
    {
        var go = new GameObject(SourceGameObjectName);
        Object.DontDestroyOnLoad(go);

        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f; // 2D - UI sounds do not pan

        // Route through the AudioMixer Sound group.
        // The mixer asset lives at Assets/Resources/
        // AudioMixer.mixer (guid
        // 3db3ee307da7e774990ddcb9bae8e59b) and has
        // had a 'Sound' group since before round 60.
        // FindMatchingGroups('Sound') returns the
        // groups whose name matches; we take the
        // first one. If the mixer or the group is
        // not found, the AudioSource plays un-routed
        // (the hover still plays, just not affected
        // by the SoundsVolume slider).
        var mixer = Resources.Load<AudioMixer>(MixerResourceName);
        if (mixer != null)
        {
            var groups = mixer.FindMatchingGroups(MixerGroupName);
            if (groups != null && groups.Length > 0)
                _source.outputAudioMixerGroup = groups[0];
        }
    }
}
