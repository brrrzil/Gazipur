using UnityEngine;
using UnityEngine.Audio;
using Zenject;

public class SoundControl : MonoBehaviour
{
     [ SerializeField] private AudioMixerGroup mixer;
    public float MusicVolume { get; private set; }
    public float SoundVolume { get; private set; }
    public bool IsMute { get; private set; }
    public void ChangeMusicVolume(float value)
    {
        MusicVolume = value;
        mixer.audioMixer.SetFloat("MusicVolume", Mathf.Log10(value) * 20);
    }
    public void ChangeSoundVolume(float value)
    {
        SoundVolume = value;
        mixer.audioMixer.SetFloat("SoundsVolume", Mathf.Log10(value) * 20);
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
    }
}
