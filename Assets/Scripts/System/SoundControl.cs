using UnityEngine;
using UnityEngine.Audio;
using Zenject;

public class SoundControl : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup mixer;
    public float MusicVolume { get; private set; } = 0.75f;
    public float SoundVolume { get; private set; } = 0.75f;
    public bool IsMute { get; private set; }

    private const string MusicKey = "MusicVolume";
    private const string SoundKey = "SoundsVolume";
    private const string MuteKey = "MasterMuted";

    private void Awake()
    {
        // BUGFIX (round 14): if the player has never opened the settings
        // panel (no PlayerPrefs key), MusicVolume/SoundVolume used to stay
        // at the C# default of 0. The slider then showed 0, but the AudioMixer
        // param was at Unity's default 0 dB (full volume) — the player heard
        // full-volume audio while the slider said "muted". Force the mixer
        // to the same default the slider shows.
        if (PlayerPrefs.HasKey(MusicKey))
            ChangeMusicVolume(PlayerPrefs.GetFloat(MusicKey));
        else
            ChangeMusicVolume(0.75f);
        if (PlayerPrefs.HasKey(SoundKey))
            ChangeSoundVolume(PlayerPrefs.GetFloat(SoundKey));
        else
            ChangeSoundVolume(0.75f);
        if (PlayerPrefs.HasKey(MuteKey))
            Mute(PlayerPrefs.GetInt(MuteKey) == 1);
    }

    public void ChangeMusicVolume(float value)
    {
        MusicVolume = value;
        // (round 33) Clamp to a tiny positive value before Log10: with
        // value == 0 the formula gives -Infinity, which AudioMixer handles
        // inconsistently (it can keep the previous dB, clip to 0, or ignore
        // the call entirely — behaviour depended on Unity version). Use a
        // small epsilon so the slider's 0 still maps to the AudioMixer's
        // effectively-silent floor of -80 dB.
        float safe = Mathf.Max(value, 0.0001f);
        mixer.audioMixer.SetFloat("MusicVolume", Mathf.Log10(safe) * 20f);
        Debug.Log($"[SoundControl] MusicVolume -> {Mathf.Log10(safe) * 20f:F1} dB (slider={value:F2})");
        SaveSettings();
    }
    public void ChangeSoundVolume(float value)
    {
        SoundVolume = value;
        // (round 33) Same fix as ChangeMusicVolume: clamp to 0.0001
        // before Log10 so the 0-slider position is silence, not -Infinity.
        float safe = Mathf.Max(value, 0.0001f);
        mixer.audioMixer.SetFloat("SoundsVolume", Mathf.Log10(safe) * 20f);
        Debug.Log($"[SoundControl] SoundsVolume -> {Mathf.Log10(safe) * 20f:F1} dB (slider={value:F2})");
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
        Debug.Log($"[SoundControl] Mute -> {isMute} (MasterVolume={(isMute ? -80 : 0)} dB)");
        SaveSettings();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.SetFloat(SoundKey, SoundVolume);
        PlayerPrefs.SetInt(MuteKey, IsMute ? 1 : 0);
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
