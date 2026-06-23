using UnityEngine;
using UnityEngine.Audio;
using Zenject;

public class SoundControl : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup mixer;
    public float MusicVolume { get; private set; }
    public float SoundVolume { get; private set; }
    public bool IsMute { get; private set; }

    // PlayerPrefs keys — shared with MenuAudioManager so menu and game agree.
    private const string MusicKey = "MusicVolume";
    private const string SoundKey = "SoundsVolume";
    // MasterMuted is intentionally NOT auto-restored. If a previous session
    // saved it as 1, the next start would silently set MasterVolume to -80 dB
    // globally, muting EVERYTHING (dialog voices, music, footsteps, etc.) —
    // which is exactly what bit us after round 6. The mute state should be
    // explicit and per-session, not persisted. See Gazipur-rules.md round 9.

    private void Awake()
    {
        // One-time cleanup of the stale 'MasterMuted' PlayerPrefs key that
        // round 6/7 left behind. Safe to drop regardless of value (0 or 1)
        // since we no longer read this key.
        if (PlayerPrefs.HasKey("MasterMuted"))
            PlayerPrefs.DeleteKey("MasterMuted");

        // Load persisted volume values. The original "pass-through" mixer
        // update is preserved (no Mathf.Clamp01 / no safe-epsilon) because
        // adding those in round 6 made the in-game slider stop responding to
        // drag — reverted per user feedback.
        if (PlayerPrefs.HasKey(MusicKey))
            ChangeMusicVolume(PlayerPrefs.GetFloat(MusicKey));
        if (PlayerPrefs.HasKey(SoundKey))
            ChangeSoundVolume(PlayerPrefs.GetFloat(SoundKey));
        // Mute state is NOT restored from PlayerPrefs. The user has to
        // toggle mute again each session if they want it. The mixer
        // MasterVolume starts at whatever the AudioMixer asset default is
        // (typically 0 dB — no attenuation).
    }

    public void ChangeMusicVolume(float value)
    {
        MusicVolume = value;
        mixer.audioMixer.SetFloat("MusicVolume", Mathf.Log10(value) * 20);
        SaveSettings();
    }
    public void ChangeSoundVolume(float value)
    {
        SoundVolume = value;
        mixer.audioMixer.SetFloat("SoundsVolume", Mathf.Log10(value) * 20);
        SaveSettings();
    }
    public void Mute(bool isMute)
    {
        IsMute = isMute;
        if (isMute)
        {
            mixer.audioMixer.SetFloat("MasterVolume", -80);
        }
        else
        {
            mixer.audioMixer.SetFloat("MasterVolume", 0);
        }
        SaveSettings();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.SetFloat(SoundKey, SoundVolume);
        // Mute state is intentionally NOT saved — see Awake() comment.
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveSettings();
    }

    private void OnApplicationQuit()
    {
        SaveSettings();
    }
}
