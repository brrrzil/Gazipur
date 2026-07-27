using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static EnumData;
using DG.Tweening;
using NaughtyAttributes;
using Zenject;

public class CharacterRemarks : MonoBehaviour
{
    [SerializeField] private CanvasGroup _cGroup;
    [SerializeField] private Text _remarkText;
    [SerializeField] private RemarkData[] _remarks;
    [SerializeField] private AudioSource _remarkAudioSource; // Используем этот источник из-за бага с Рахулом

    private Tween _tween;
    [Inject] private DataManager _data;
    private bool _isStarted;
    private RemarksType _currentType;

    private AudioClip _lastPlayedClip;
    private float _lastPlayTime;

    private void Awake()
    {
        // Если не назначен в инспекторе, создаем автоматически
        if (_remarkAudioSource == null)
        {
            _remarkAudioSource = gameObject.AddComponent<AudioSource>();
            _remarkAudioSource.spatialBlend = 0f;
            _remarkAudioSource.volume = 1f;
            _remarkAudioSource.playOnAwake = false;
            _remarkAudioSource.loop = false;
        }
    }

    [System.Serializable]
    public class RemarkData
    {
        [TextArea] public string remark;
        public AudioClip voice;
        public bool isMultiRemark;
        [ShowIf("isMultiRemark"), AllowNesting, TextArea] public string remarkAnyTime;
        [ShowIf("isMultiRemark"), AllowNesting] public AudioClip voiceAnyTime;
        public int chance;
        public float showTime = 3;
        public RemarksType type;
        public bool isOneTime;
        [HideInInspector] public bool hasBeen;
    }

    public bool StartRemark(RemarksType remark)
    {
        if (remark == RemarksType.rohulHelp && _data.gameMode == GameMode.dialog)
        {
            return false;
        }

        var rem = System.Array.Find(_remarks, i => i.type == remark);

        if (rem == null) return false;

        int rnd = Random.Range(0, 100);

        if (rem.chance < rnd)
            return false;

        if (_isStarted && _currentType == remark)
            return false;

        _currentType = rem.type;
        _isStarted = true;

        if (rem.isOneTime)
            rem.chance = 0;

        if (!rem.isMultiRemark)
        {
            _remarkText.text = rem.remark;
            PlayVoice(rem.voice);
        }
        else
        {
            _remarkText.text = rem.hasBeen ? rem.remarkAnyTime : rem.remark;
            PlayVoice(rem.hasBeen ? rem.voiceAnyTime : rem.voice);
        }
        rem.hasBeen = true;
        _tween?.Kill();
        _tween = _cGroup.DOFade(1, 0.5f).OnComplete(() =>
        {
            _tween = _cGroup.DOFade(0, 0.5f).SetDelay(rem.showTime).OnComplete(() => _isStarted = false);
        });
        return true;
    }

    private void PlayVoice(AudioClip clip)
    {
        if (!clip) return;

        if (_lastPlayedClip == clip && Time.time - _lastPlayTime < 0.3f)
            return;

        if (_remarkAudioSource == null)
        {
            Debug.LogError("CharacterRemarks: _remarkAudioSource is null!");
            return;
        }

        if (_remarkAudioSource.isPlaying)
            _remarkAudioSource.Stop();

        _remarkAudioSource.clip = clip;
        _remarkAudioSource.Play();

        _lastPlayedClip = clip;
        _lastPlayTime = Time.time;
    }
}