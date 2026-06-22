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
        // ��������� ����������� ���������
        // BUGFIX: was reading "MenuMusicVolume"/"MenuSoundsVolume" but writing
        // "MusicVolume"/"SoundsVolume" — the read keys never had values, so the
        // menu always reset to 0.75 on every launch. Use the same keys both ways.
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        float savedSounds = PlayerPrefs.GetFloat("SoundsVolume", 0.75f);
        int musicMuted = PlayerPrefs.GetInt("MusicMuted", 0);
        int soundsMuted = PlayerPrefs.GetInt("SoundsMuted", 0);

        // ������������� UI
        musicSlider.value = savedMusic;
        soundsSlider.value = savedSounds;
        musicToggle.isOn = musicMuted == 0;
        soundsToggle.isOn = soundsMuted == 0;

        // ��������� ���������
        ApplyMusicVolume(savedMusic, musicMuted == 1);
        ApplySoundsVolume(savedSounds, soundsMuted == 1);

        // ������������� �� �������
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
        // BUGFIX (K3 follow-up): the menu mixer has the same exposed parameter
        // names as the in-game mixer — "MusicVolume" / "SoundsVolume", not
        // "Music" / "Sound". Setting a non-existent param logged
        // "Exposed name does not exist" every time the slider moved.
        float finalVolume = muted ? 0.0001f : volume;
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(finalVolume) * 20);
    }

    private void ApplySoundsVolume(float volume, bool muted)
    {
        float finalVolume = muted ? 0.0001f : volume;
        audioMixer.SetFloat("SoundsVolume", Mathf.Log10(finalVolume) * 20);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
        PlayerPrefs.SetFloat("SoundsVolume", soundsSlider.value);
        PlayerPrefs.SetInt("MusicMuted", musicToggle.isOn ? 0 : 1);
        PlayerPrefs.SetInt("SoundsMuted", soundsToggle.isOn ? 0 : 1);
        PlayerPrefs.Save();
    }
}