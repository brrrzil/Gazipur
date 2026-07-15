using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class MenuAudioManager : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundsSlider;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle soundsToggle;

    private void Start()
    {
        float savedMusic = PlayerPrefs.GetFloat("MenuMusicVolume", 0.75f);
        float savedSounds = PlayerPrefs.GetFloat("MenuSoundsVolume", 0.75f);
        int musicMuted = PlayerPrefs.GetInt("MenuMusicMuted", 0);
        int soundsMuted = PlayerPrefs.GetInt("MenuSoundsMuted", 0);

        musicSlider.value = savedMusic;
        soundsSlider.value = savedSounds;
        musicToggle.isOn = musicMuted == 0;
        soundsToggle.isOn = soundsMuted == 0;

        ApplyMusicVolume(savedMusic, musicMuted == 1);
        ApplySoundsVolume(savedSounds, soundsMuted == 1);

        musicSlider.onValueChanged.AddListener(OnMusicSlider);
        soundsSlider.onValueChanged.AddListener(OnSoundsSlider);
        musicToggle.onValueChanged.AddListener(OnMusicToggle);
        soundsToggle.onValueChanged.AddListener(OnSoundsToggle);
    }

    private void OnMusicSlider(float value)
    {
        ApplyMusicVolume(value, !musicToggle.isOn);
        SaveSettings();
    }

    private void OnSoundsSlider(float value)
    {
        ApplySoundsVolume(value, !soundsToggle.isOn);
        SaveSettings();
    }

    private void OnMusicToggle(bool isOn)
    {
        ApplyMusicVolume(musicSlider.value, !isOn);
        SaveSettings();
    }

    private void OnSoundsToggle(bool isOn)
    {
        ApplySoundsVolume(soundsSlider.value, !isOn);
        SaveSettings();
    }

    private void ApplyMusicVolume(float volume, bool muted)
    {
        // (round 42) Clamp volume to a tiny positive value even when
        // not muted, so Mathf.Log10 doesn't return -Infinity. AudioMixer
        // handles -Infinity inconsistently. 0.0001 -> -80 dB, which is
        // below the AudioMixer's effectively-silent floor.
        float finalVolume = muted ? 0.0001f : Mathf.Max(volume, 0.0001f);
        // BUGFIX: Resources/AudioMixer.mixer only exposes MasterVolume /
        // SoundsVolume / MusicVolume. The pre-round-6 code wrote to
        // "Music" / "Sound" which threw 'Exposed name does not exist' on
        // every slider tick. Match the in-game SoundControl param names.
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(finalVolume) * 20);
    }

    private void ApplySoundsVolume(float volume, bool muted)
    {
        // (round 42) Same clamp as ApplyMusicVolume: 0.0001 floor so
        // the slider's 0 position is silence, not -Infinity.
        float finalVolume = muted ? 0.0001f : Mathf.Max(volume, 0.0001f);
        audioMixer.SetFloat("SoundsVolume", Mathf.Log10(finalVolume) * 20);
    }

    private void SaveSettings()
    {
        // (round 42) Use the same PlayerPrefs keys that Start() reads
        // from ("MenuMusicVolume", "MenuSoundsVolume", "MenuMusicMuted",
        // "MenuSoundsMuted"). The previous version wrote to "MusicVolume"
        // and "MusicMuted" (no "Menu" prefix), which meant the toggle
        // state was lost across sessions: Start() never saw what the
        // user toggled because it was reading from a different key.
        PlayerPrefs.SetFloat("MenuMusicVolume", musicSlider.value);
        PlayerPrefs.SetFloat("MenuSoundsVolume", soundsSlider.value);
        PlayerPrefs.SetInt("MenuMusicMuted", musicToggle.isOn ? 0 : 1);
        PlayerPrefs.SetInt("MenuSoundsMuted", soundsToggle.isOn ? 0 : 1);
        PlayerPrefs.Save();
    }
}
