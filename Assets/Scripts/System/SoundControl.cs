using UnityEngine;
using UnityEngine.Audio;
using Zenject;

public class SoundControl : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup mixer;

    public float MusicVolume { get; private set; } = 0.75f;
    public float SoundVolume { get; private set; } = 0.75f;
    public bool IsMute { get; private set; }

    // PlayerPrefs keys — shared with MenuAudioManager so menu and game agree.
    private const string MusicKey = "MusicVolume";
    private const string SoundKey = "SoundsVolume";
    private const string MuteKey = "MasterMuted";

    private void Awake()
    {
        // Load persisted values and apply to the mixer before any audio plays.
        // BUGFIX (K4): the old code passed Mathf.Log10(0) = -Infinity to the
        // mixer, which AudioMixer.SetFloat rejects and Unity spams a warning.
        // BUGFIX (K5): the old code never persisted anything, so every launch
        // started at the default 0.75 regardless of what the player set.
        if (PlayerPrefs.HasKey(MusicKey))
            ChangeMusicVolume(PlayerPrefs.GetFloat(MusicKey));
        else
            ApplyMusicMixer(MusicVolume);

        if (PlayerPrefs.HasKey(SoundKey))
            ChangeSoundVolume(PlayerPrefs.GetFloat(SoundKey));
        else
            ApplySoundMixer(SoundVolume);

        if (PlayerPrefs.HasKey(MuteKey))
            Mute(PlayerPrefs.GetInt(MuteKey) == 1);
        else
            Mute(false);
    }

    public void ChangeMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyMusicMixer(MusicVolume);
        SaveSettings();
    }

    public void ChangeSoundVolume(float value)
    {
        SoundVolume = Mathf.Clamp01(value);
        ApplySoundMixer(SoundVolume);
        SaveSettings();
    }

    public void Mute(bool isMute)
    {
        IsMute = isMute;
        if (isMute)
        {
            mixer.audioMixer.SetFloat("MasterVolume", -80f);
        }
        else
        {
            mixer.audioMixer.SetFloat("MasterVolume", 0f);
        }
        SaveSettings();
    }

    // Helper that maps a 0..1 linear value to the mixer's dB scale, safely.
    // BUGFIX (K4): clamping the linear value to a tiny positive epsilon avoids
    // Mathf.Log10(0) returning -Infinity, which AudioMixer.SetFloat rejects.
    private void ApplyMusicMixer(float value)
    {
        float safe = Mathf.Max(0.0001f, value);
        mixer.audioMixer.SetFloat("MusicVolume", Mathf.Log10(safe) * 20f);
    }

    private void ApplySoundMixer(float value)
    {
        float safe = Mathf.Max(0.0001f, value);
        mixer.audioMixer.SetFloat("SoundsVolume", Mathf.Log10(safe) * 20f);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.SetFloat(SoundKey, SoundVolume);
        PlayerPrefs.SetInt(MuteKey, IsMute ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Persist on app suspend/quit (mobile + editor stop play mode).
    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveSettings();
    }

    private void OnApplicationQuit()
    {
        SaveSettings();
    }
}
