
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;
using DG.Tweening;

public class Sounds : MonoBehaviour
{
    public static Sounds chooseSound { get; private set; }
    [SerializeField] private AudioMixerGroup mixer;
    public AudioSource simpleGameBack;
    public AudioSource startMenu;
    public AudioSource finishTrack;
    public AudioSource takeCard;
    public AudioSource getCard;
    public AudioSource putCard;
    public AudioSource closeCard;
    public AudioSource toBleed;
    public AudioSource butonClick;
    public AudioSource buy;
    public AudioSource potion;    
    public AudioSource openInfo;
    public AudioSource combo;
    public AudioSource comboComplete;
    public AudioSource enemyFinish;
    public AudioSource finish;
    public AudioSource useDemon;
    public AudioSource damage;
    public AudioSource hero;
    public AudioSource changePage;
    public AudioSource selectCard;
    public AudioSource takeReward;
    public AudioSource setReward;
    public AudioSource damageUp;
    public AudioSource relicUse;
    public AudioSource debufUse;
    public AudioSource bossCard;
    public AudioSource showFinishPanel;
    public AudioSource[] mapBackGround;

    private AudioSource _curBackground;
    [Inject]
    private void Init()
    {
        if (chooseSound == null)
        {
            chooseSound = this;
        }
        else if(chooseSound == this)
        {
            Destroy(gameObject);
        }           

        DontDestroyOnLoad(gameObject);        
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
        mixer.audioMixer.SetFloat("SoundsVolume", Mathf.Log10(volume)*20);
    }
    public void SetSoundsVolume(float volume)
    {        
        mixer.audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
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
            case 0: butonClick.Play();
               break;
            //case 2: buyAtCoins.Play();
            //    break;
            //default: otherButtons.Play();
            //    break;
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
