
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;
using Zenject;
using static EnumData;

public class Sounds : MonoBehaviour
{
    [field: SerializeField] public AudioSource DialogSource { get; private set;}
    [SerializeField] private AudioMixerGroup mixer;

    [SerializeField] private AudioSource _playerSource;
    [SerializeField] private PlayerSoundData[] _playerSounds;
    [SerializeField] private AudioSource _uiSource;
    [SerializeField] private UISoundData[] _uiSound;
    [field: SerializeField] public AudioSource[] Background { get; private set; }

    private AudioSource _curBackground;

    [Inject]
    private void Init()
    {
        // BUGFIX: DontDestroyOnLoad only works on root GameObjects. The
        // Sounds component lives on SoundManager.prefab, which is nested
        // inside GameManager.prefab — so 'gameObject' is a CHILD, not a
        // root, and the call throws 'DontDestroyOnLoad only works for
        // root GameObjects' on every scene load. Use transform.root to
        // grab the actual top of the hierarchy (GameManager itself,
        // which IS a root in the scene).
        DontDestroyOnLoad(transform.root.gameObject);
    }

    [System.Serializable]
    public struct PlayerSoundData
    {
        public PlayerSound sound;
        public AudioClip clip;
    }

    [System.Serializable]
    public struct UISoundData
    {
        public UISound sound;
        public AudioClip clip;
    }

    private void Start()
    {
        // (round 35) Null-guards: Background[] may be empty / null if
        // SoundManager.prefab wasn't fully wired.
        if (Background == null) return;
        foreach (var bg in Background)
        {
            if (bg != null) bg.Stop();
        }
        if (Background.Length > 0 && Background[0] != null)
            Background[0].Play();
    }

    public void RandomPitch(AudioSource pitchedAudio, float spread)
    {
        float pitch = Random.Range(-spread, spread);
        pitchedAudio.pitch = 1 + pitch;
        if (!pitchedAudio.isPlaying)
        {
            pitchedAudio.Play();
        }
        else if (pitchedAudio.time > 0.1f)
        {
            pitchedAudio.Play();
        }
    }

    public void ButtonClick(int typeNumber)
    {
        switch (typeNumber)
        {
            case 0: UIPlay(UISound.buttonClick);
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
        source.DOFade(1, 3);
        _curBackground.DOFade(0, 3).OnComplete(() =>
        {
            _curBackground.Stop();
            _curBackground = source;
        });
    }

    public void PlayerPlay(PlayerSound sound, bool isLoop)
    {
        // (round 35) Null-guards: _playerSource or _playerSounds may be
        // unassigned if SoundManager.prefab wasn't fully wired in the
        // editor. Without these checks every play call would throw
        // NullReferenceException / ArgumentNullException.
        if (_playerSource == null || _playerSounds == null) return;
        var found = System.Array.Find(_playerSounds, s => s.sound == sound);
        if (found.clip == null) return;
        _playerSource.clip = found.clip;
        _playerSource.loop = isLoop;
        _playerSource.Play();
    }

    public void PlayerStop()
    {
        if (_playerSource == null) return;
        _playerSource.loop = false;
        _playerSource.Stop();
    }

    public void UIPlay(UISound sound)
    {
        // (round 35) Same null-guards as PlayerPlay.
        if (_uiSource == null || _uiSound == null) return;
        var found = System.Array.Find(_uiSound, s => s.sound == sound);
        if (found.clip == null) return;
        _uiSource.clip = found.clip;
        _uiSource.Play();
    }

    public void OpenMenu()
    {
        UIPlay(UISound.openPanel);
    }
}