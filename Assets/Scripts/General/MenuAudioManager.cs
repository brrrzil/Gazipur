using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class MenuAudioManager : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundsSlider;
    // Round 78: one Toggle for both channels instead of two.
    // Previously the menu had separate 'musicToggle' and
    // 'soundsToggle' for 'mute music' and 'mute sounds' but the
    // user has consolidated them into a single 'muteToggle'
    // that mutes / unmutes BOTH channels at once. The
    // individual toggle SerializeFields are removed from the
    // class and the toggle wiring in Start and the
    // per-channel handler methods (OnMusicToggle,
    // OnSoundsToggle) are replaced by a single OnMuteToggle.
    // PlayerPrefs keys for muted state are also consolidated
    // from two keys ('MenuMusicMuted', 'MenuSoundsMuted')
    // to one ('MenuMuted') - users who had a different mute
    // state for music and sounds before this change will
    // see both channels snap to the saved value on next
    // launch, which is the right behaviour for a single-
    // mute model.
    [SerializeField] private Toggle muteToggle;

    private void Start()
    {
        float savedMusic = PlayerPrefs.GetFloat("MenuMusicVolume", 0.75f);
        float savedSounds = PlayerPrefs.GetFloat("MenuSoundsVolume", 0.75f);
        int muted = PlayerPrefs.GetInt("MenuMuted", 0);

        musicSlider.value = savedMusic;
        soundsSlider.value = savedSounds;
        // isOn = true means 'Mute is on' = sound is muted.
        // So isOn maps to the muted flag directly (not its
        // inverse). One toggle drives both channels now, so
        // the previous 'musicToggle.isOn = musicMuted == 1'
        // and 'soundsToggle.isOn = soundsMuted == 1' pair
        // is collapsed into one assignment.
        muteToggle.isOn = muted == 1;

        ApplyMusicVolume(savedMusic, muted == 1);
        ApplySoundsVolume(savedSounds, muted == 1);

        musicSlider.onValueChanged.AddListener(OnMusicSlider);
        soundsSlider.onValueChanged.AddListener(OnSoundsSlider);
        // One listener for the single mute toggle.
        muteToggle.onValueChanged.AddListener(OnMuteToggle);
    }

    private void OnMusicSlider(float value)
    {
        // muted = isOn (Mute toggle semantics: isOn = muted).
        // The single muteToggle drives both ApplyMusicVolume
        // and ApplySoundsVolume, so music slider movement
        // does not re-apply sounds volume and vice versa -
        // the toggle is the only path that touches both.
        ApplyMusicVolume(value, muteToggle.isOn);
        SaveSettings();
    }

    private void OnSoundsSlider(float value)
    {
        ApplySoundsVolume(value, muteToggle.isOn);
        SaveSettings();
    }

    private void OnMuteToggle(bool isOn)
    {
        // One toggle drives both channels. ApplyMusicVolume
        // and ApplySoundsVolume are called back-to-back with
        // the current slider values, so the user hears
        // both music and sounds go silent (or both come back)
        // in the same frame.
        ApplyMusicVolume(musicSlider.value, isOn);
        ApplySoundsVolume(soundsSlider.value, isOn);
        SaveSettings();
    }

    private void ApplyMusicVolume(float volume, bool muted)
    {
        // Clamp volume to a tiny positive value even when
        // not muted, so Mathf.Log10 doesn't return -Infinity.
        // AudioMixer handles -Infinity inconsistently.
        // 0.0001 -> -80 dB, which is below the AudioMixer's
        // effectively-silent floor.
        float finalVolume = muted ? 0.0001f : Mathf.Max(volume, 0.0001f);
        // Resources/AudioMixer.mixer only exposes MasterVolume /
        // SoundsVolume / MusicVolume. Match the in-game
        // SoundControl param names.
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(finalVolume) * 20);
    }

    private void ApplySoundsVolume(float volume, bool muted)
    {
        // Same clamp as ApplyMusicVolume: 0.0001 floor so
        // the slider's 0 position is silence, not -Infinity.
        float finalVolume = muted ? 0.0001f : Mathf.Max(volume, 0.0001f);
        audioMixer.SetFloat("SoundsVolume", Mathf.Log10(finalVolume) * 20);
    }

    private void SaveSettings()
    {
        // Single 'MenuMuted' key replaces 'MenuMusicMuted'
        // and 'MenuSoundsMuted'. Volume slider values are
        // unchanged ('MenuMusicVolume', 'MenuSoundsVolume').
        // (Round 42 fix) Use the same PlayerPrefs keys that
        // Start() reads from; the previous version wrote
        // without the 'Menu' prefix and the toggle state
        // was lost across sessions.
        // isOn = true means muted, so store 1 (muted) when
        // isOn is true, 0 (unmuted) when isOn is false.
        PlayerPrefs.SetFloat("MenuMusicVolume", musicSlider.value);
        PlayerPrefs.SetFloat("MenuSoundsVolume", soundsSlider.value);
        PlayerPrefs.SetInt("MenuMuted", muteToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }
}
