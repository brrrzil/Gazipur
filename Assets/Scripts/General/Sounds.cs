using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;
using Zenject;
using static EnumData;

public class Sounds : MonoBehaviour
{
    [field: SerializeField] public AudioSource DialogSource { get; private set; }
    [SerializeField] private AudioMixerGroup mixer;

    [SerializeField] private AudioSource _playerSource;
    [SerializeField] private PlayerSoundData[] _playerSounds;
    [SerializeField] private AudioSource _uiSource;
    [SerializeField] private UISoundData[] _uiSound;
    [field: SerializeField] public AudioSource[] Background { get; private set; }

    // Фоновые треки для разных состояний (можно назначить в инспекторе)
    [Header("Background Tracks")]
    [SerializeField] private AudioSource _menuBackground;
    [SerializeField] private AudioSource _gameBackground;
    [SerializeField] private AudioSource _dieBackground;
    [SerializeField] private AudioSource _winBackground;

    private AudioSource _curBackground;
    private AudioSource _targetBackground;

    [Inject]
    private void Init()
    {
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
        if (Background == null) return;
        foreach (var bg in Background)
        {
            if (bg != null) bg.Stop();
        }

        // Стартуем с игровым фоном
        if (_gameBackground != null)
        {
            _curBackground = _gameBackground;
            _curBackground.Play();
        }
        else if (Background.Length > 0 && Background[0] != null)
        {
            _curBackground = Background[0];
            _curBackground.Play();
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
        else if (pitchedAudio.time > 0.1f)
        {
            pitchedAudio.Play();
        }
    }

    public void ButtonClick(int typeNumber)
    {
        switch (typeNumber)
        {
            case 0:
                UIPlay(UISound.buttonClick);
                break;
        }
    }

    // --- НОВЫЕ МЕТОДЫ ДЛЯ ПЕРЕКЛЮЧЕНИЯ ФОНА ---

    // Rate-limit set so 'X not assigned' warnings don't spam the Console.
    // A missing AudioSource reference is harmless (the music simply
    // doesn't play that track) and re-logging it every frame drowns out
    // real errors.
    private int _lastWarnFrame = -1;

    private void WarnOncePerSecond(string msg)
    {
        if (Time.frameCount - _lastWarnFrame < 60) return;
        _lastWarnFrame = Time.frameCount;
        Debug.LogWarning(msg);
    }

    /// <summary>
    /// Переключить фоновую музыку на трек для меню
    /// </summary>
    public void SwitchToMenuBackground()
    {
        if (_menuBackground != null)
            ChangeBackground(_menuBackground);
        else
            WarnOncePerSecond("Sounds: _menuBackground not assigned!");
    }

    /// <summary>
    /// Переключить фоновую музыку на трек для игры
    /// </summary>
    public void SwitchToGameBackground()
    {
        if (_gameBackground != null)
            ChangeBackground(_gameBackground);
        else
            WarnOncePerSecond("Sounds: _gameBackground not assigned!");
    }

    /// <summary>
    /// Переключить фоновую музыку на трек для смерти
    /// </summary>
    public void SwitchToDieBackground()
    {
        if (_dieBackground != null)
            ChangeBackground(_dieBackground);
        else
            WarnOncePerSecond("Sounds: _dieBackground not assigned!");
    }

    /// <summary>
    /// Переключить фоновую музыку на трек для победы
    /// </summary>
    public void SwitchToWinBackground()
    {
        if (_winBackground != null)
            ChangeBackground(_winBackground);
        else
            WarnOncePerSecond("Sounds: _winBackground not assigned!");
    }

    /// <summary>
    /// Остановить фоновую музыку
    /// </summary>
    public void StopBackground()
    {
        if (_curBackground != null)
        {
            _curBackground.DOFade(0, 1f).OnComplete(() =>
            {
                _curBackground.Stop();
                _curBackground = null;
            });
        }
    }

    // --- КОНЕЦ НОВЫХ МЕТОДОВ ---

    public void ChangeBackground(AudioSource source)
    {
        if (source == null) return;

        if (!_curBackground)
        {
            _curBackground = source;
            source.volume = 1f;
            source.Play();
            return;
        }
        if (_curBackground == source) return;
        FadeSound(source);
    }

    public void OverlapBackground(AudioSource source)
    {
        if (source == null) return;

        float tr = _curBackground != null ? _curBackground.time : 0;
        if (_curBackground != null)
            _curBackground.Stop();
        _curBackground = source;
        _curBackground.time = tr;
        _curBackground.volume = 1f;
        _curBackground.Play();
    }

    private void FadeSound(AudioSource source)
    {
        if (source == null) return;

        source.volume = 0;
        source.Play();
        source.DOFade(1, 3);

        if (_curBackground != null)
        {
            _curBackground.DOFade(0, 3).OnComplete(() =>
            {
                if (_curBackground != null)
                    _curBackground.Stop();
                _curBackground = source;
            });
        }
        else
        {
            _curBackground = source;
        }
    }

    public void PlayerPlay(PlayerSound sound, bool isLoop)
    {
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