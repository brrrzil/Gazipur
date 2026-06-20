
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;
using DG.Tweening;

public class Sounds : MonoBehaviour
{
    public static Sounds ChooseSound { get; private set; }
    [SerializeField] private AudioMixerGroup mixer;

    [field: SerializeField] public AudioSource ButonClick { get; private set; }
    [field:SerializeField] public AudioSource[] BackGround { get; private set; }

    private AudioSource _curBackground;
    [Inject]
    private void Init()
    {
        if (ChooseSound == null)
        {
            ChooseSound = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (ChooseSound == this)
        {
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void RandomPitch(AudioSource pitchedAudio, float spread)
    {
        float pitch = Random.Range(-spread, spread);
        pitchedAudio.pitch = 1 + pitch;
        if (!pitchedAudio.isPlaying)
        {
            pitchedAudio.Play();
        }
        else if(pitchedAudio.time>0.1f)
        {
            pitchedAudio.Play();
        }
    }
    public void SetMusicVolume(float volume)
    {
        mixer.audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
    }
    public void SetSoundsVolume(float volume)
    {
        mixer.audioMixer.SetFloat("SoundsVolume", Mathf.Log10(volume) * 20);
    }
    public void Mute(bool mute)
    {
        if (mute)
        {
            mixer.audioMixer.SetFloat("MasterVolume", -80);
        }
        else
        {
            mixer.audioMixer.SetFloat("MasterVolume", 0);
        }
    }
    public void ButtonClick(int typeNumber)
    {
        switch(typeNumber)
        {
            case 0: ButonClick.Play();
               break;
        }
    }
    public void ChangeBackground(AudioSource source)
    {
        if (!_curBackground)
        {
            _curBackground = source;
            source.Play();
            return;
        }
        if (_curBackground == source) return;
        FadeSound(source);
    }
    public void OverlapBackground(AudioSource source)
    {
        float tr = _curBackground.time;
        _curBackground.Stop();
        _curBackground = source;
        _curBackground.time = tr;
        _curBackground.Play();
    }
    private void FadeSound(AudioSource source)
    {
        source.volume = 0;
        source.Play();
        source.DOFade(1, 1);        
        _curBackground.DOFade(0, 1).OnComplete(() =>
        {
            _curBackground.Stop();
            _curBackground = source;
        });        
    }
}
