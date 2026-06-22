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
    private const string MuteKey = "MasterMuted";

    private void Awake()
    {
        // Load persisted values, but keep the original "pass-through" mixer
        // update (no Mathf.Clamp01 / no safe-epsilon). The previous round
        // added those for K4 and the slider stopped responding to drag —
        // reverted per user feedback.
        if (PlayerPrefs.HasKey(MusicKey))
            ChangeMusicVolume(PlayerPrefs.GetFloat(MusicKey));
        if (PlayerPrefs.HasKey(SoundKey))
            ChangeSoundVolume(PlayerPrefs.GetFloat(SoundKey));
        if (PlayerPrefs.HasKey(MuteKey))
            Mute(PlayerPrefs.GetInt(MuteKey) == 1);
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
