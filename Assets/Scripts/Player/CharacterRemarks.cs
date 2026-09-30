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
            Debug.Log("CharacterRemarks: Created new AudioSource automatically");
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

        // (r5 / remark-persistence) One-time remarks have their play
        // history persisted to PlayerPrefs so a scene reload (or
        // Continue) doesn't replay them. The previous code only
        // zeroed the chance for the lifetime of the CharacterRemarks
        // MonoBehaviour - on Awake after Continue the chance field
        // was back to its authored value and the remark fired again.
        // Use a stable key per remark type so the PlayerPrefs entry
        // survives across sessions; clearing it on New Game is the
        // caller's responsibility (DeleteSave wipes everything).
        if (rem.isOneTime && PlayerPrefs.GetInt("remark_played_" + (int)remark, 0) == 1)
        {
            return false;
        }

        int rnd = Random.Range(0, 100);

        if (rem.chance < rnd)
            return false;

        if (_isStarted && _currentType == remark)
            return false;

        _currentType = rem.type;
        _isStarted = true;

        if (rem.isOneTime)
            rem.chance = 0;

        // (r5 / nre-fix) Unity-null guard on _remarkText and _cGroup.
        // They are SerializeField references into the Canvas hierarchy;
        // if the host [GameScene (DDOL)] was deactivated between when
        // the PlayerState coroutine queued this remark and when it
        // actually ran, the underlying UI Text has been destroyed
        // and assigning .text throws MissingReferenceException. The
        // remark is non-essential cosmetic UI - skip cleanly rather
        // than crash the coroutine.
        if (_remarkText == null || _cGroup == null)
        {
            Debug.LogWarning($"[CharacterRemarks] UI refs null, skipping remark {(int)remark}");
            return false;
        }

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
        // Persist play state so the next Continue doesn't replay.
        if (rem.isOneTime)
        {
            PlayerPrefs.SetInt("remark_played_" + (int)remark, 1);
            PlayerPrefs.Save();
        }
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