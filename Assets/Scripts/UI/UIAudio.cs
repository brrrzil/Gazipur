using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;

/// <summary>
/// Round 75: central, persistent AudioSource for UI feedback
/// (button hover, button click, etc.).
///
/// Background:
///
/// Before this class, every project attempt to add a UI
/// hover/click sound hit one of two dead ends:
///
///   1. Sounds.UIPlay(UISound.buttonHover) via the project's
///      central Sounds service - needs the central
///      Sounds._uiSound[] table to be populated AND Zenject
///      to be wired with a non-null
///      GameInstaller._sounds reference. In this project
///      neither was true: GameManager.prefab had
///      _sounds = null, SoundManager.prefab had
///      _uiSound = [], and the SoundManager prefab itself
///      is not instantiated in either scene.
///   2. AudioSource.PlayClipAtPoint(clip, transform.position)
///      from a per-button fallback in ButtonAnimation -
///      the previous round's workaround. Plays fine but
///      creates a fresh throwaway GameObject per hover,
///      does not route through the project's AudioMixer,
///      and is therefore not affected by the SoundsVolume
///      slider. The user reported the resulting 'one-shot
///      audio' GameObjects piling up in the hierarchy in
///      play mode.
///
/// What this class does:
///
///   - On Awake, finds the active EventSystem (one always
///     exists in any scene that has UI). If none exists,
///     creates one so this class is self-sufficient.
///   - Adds a single AudioSource to the EventSystem's
///     GameObject. The AudioSource is the central, shared
///     UI playback target - every ButtonAnimation (and
///     any future UI sound trigger) calls PlayOneShot on
///     this source.
///   - Routes the AudioSource through the AudioMixer's
///     'Sound' group, loaded from
///     Resources/AudioMixer.mixer. The Sound group is the
///     same group Sounds.cs already targets for buy/sell/
///     buttonClick, so UI feedback is consistent with the
///     rest of the project's UI audio and obeys the
///     SoundsVolume slider.
///   - Survives scene loads via DontDestroyOnLoad so the
///     source (and the routing through the Sound group)
///     stays set up across MainMenu -> GameScene -> pause
///     panel transitions.
///   - Exposes a single public static 'Instance' and a
///     'Play' method that ButtonAnimation calls with a
///     hover AudioClip. Play uses PlayOneShot, so multiple
///     rapid hovers overlap correctly (no truncation by
///     _uiSource.clip = found.clip; _uiSource.Play() the way
///     Sounds.UIPlay does).
/// </summary>
[DefaultExecutionOrder(-100)]
public class UIAudio : MonoBehaviour
{
    public static UIAudio Instance { get; private set; }

    private AudioSource _source;

    void Awake()
    {
        // Singleton guard. If a second UIAudio ever wakes up
        // (e.g. a duplicate accidentally placed in a scene),
        // destroy the new one and keep the existing instance
        // - we want exactly one AudioSource, exactly one
        // routing to the Sound mixer group.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Find or create the EventSystem. Every scene with
        // Canvas/UI in this project already has one, but
        // creating one defensively keeps this component
        // droppable into any scene without scene-side setup.
        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
        }

        // The EventSystem lives on its own GameObject. We
        // attach the AudioSource to that GameObject so it
        // shares the EventSystem's lifetime. If the
        // EventSystem is destroyed, the AudioSource goes
        // with it; if it survives, so does the source.
        if (eventSystem.gameObject != gameObject)
        {
            // Re-parent this UIAudio MonoBehaviour to the
            // EventSystem's GameObject so the AudioSource we
            // add is on the same node. Without this, calling
            // gameObject.AddComponent<AudioSource>() below
            // would add the source to UIAudio's own
            // (auto-created) GameObject, not to EventSystem,
            // and the EventSystem would lose its source when
            // the scene tears down the standalone UIAudio
            // GameObject.
            transform.SetParent(eventSystem.transform, worldPositionStays: false);
        }

        // One AudioSource, shared, plays-on-awake off. The
        // source lives on the EventSystem's GameObject after
        // the reparent above, so this is the same component
        // we will PlayOneShot from for the rest of the
        // session.
        _source = eventSystem.gameObject.GetComponent<AudioSource>();
        if (_source == null)
            _source = eventSystem.gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f; // 2D - UI sounds should not pan

        // Route through the AudioMixer Sound group. The
        // mixer asset is at Resources/AudioMixer.mixer and
        // has been in the project since before round 60.
        // FindMatchingGroups('Sound') returns the groups
        // whose name matches; we take the first one (the
        // exact group whose fileID 2634617217559612055 the
        // existing project AudioSources point at).
        var mixer = Resources.Load<AudioMixer>("AudioMixer");
        if (mixer != null)
        {
            var groups = mixer.FindMatchingGroups("Sound");
            if (groups != null && groups.Length > 0)
                _source.outputAudioMixerGroup = groups[0];
        }
        // If the mixer or the group is not found, the
        // AudioSource simply plays un-routed. The
        // SoundsVolume slider will not affect it in that
        // case, but the hover still plays. This is the same
        // fallback behaviour Sounds.cs has for the
        // MixerGroup null case.

        // Keep this GameObject (and the AudioSource we just
        // configured) alive across scene loads so the
        // mixer routing we set up here is not redone every
        // time a new scene activates. The EventSystem
        // itself is per-scene in this project, but a
        // UIAudio instance placed in a scene's
        // DontDestroyOnLoad-aware loader (or spawned
        // manually at boot) would already survive; the
        // DontDestroyOnLoad here is the conservative
        // fallback that matches the project's Sounds
        // singleton pattern.
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Play a one-shot UI clip through the central
    /// AudioSource. Null-safe. Multiple rapid calls
    /// overlap correctly (PlayOneShot does not truncate
    /// the previous play).
    /// </summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || _source == null) return;
        _source.PlayOneShot(clip);
    }
}
