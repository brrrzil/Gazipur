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

    private Tween _tween;
    private Sequence _pendingVoiceSeq;
    [Inject] Sounds _sounds;
    private AudioSource _speaker => _sounds.DialogSource;
    private bool _isStarted;
    private RemarksType _currentType;
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

        // Cancel any pending voice from a previous remark — otherwise a queued
        // sequence can fire AFTER the speaker has been stopped (e.g. when the
        // player closes a shop mid-sentence and a leftover voice plays back
        // seconds later from the queued clip).
        _pendingVoiceSeq?.Kill();
        _pendingVoiceSeq = null;

        if (_speaker.isPlaying)
        {
            Sequence sequence = DOTween.Sequence();
            float remainingTime = _speaker.clip.length - _speaker.time;
            sequence.AppendInterval(remainingTime);
            sequence.OnComplete(() =>
            {
                _speaker.clip = clip;
                _speaker.Play();
                _pendingVoiceSeq = null;
            });
            _pendingVoiceSeq = sequence;
        }
        else
        {
            _speaker.clip = clip;
            _speaker.Play();
        }
    }

    /// <summary>
    /// Cancel any pending (queued) voice. Call this when the speaker is being
    /// stopped externally (e.g. game mode change) to prevent a stale voice
    /// from firing after the speaker is already silent.
    /// </summary>
    public void CancelPendingVoice()
    {
        _pendingVoiceSeq?.Kill();
        _pendingVoiceSeq = null;
    }
}