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

        // Cut off whatever is currently playing and start the new clip right
        // away. The previous implementation used a DOTween sequence to wait for
        // the current clip to finish, but that left a "queued" voice alive
        // even after _speaker.Stop() was called externally (e.g. on game mode
        // change to outdors) — so a leftover voice could fire several seconds
        // after the player already left the context. Stopping and starting
        // immediately avoids that without needing to track sequences.
        if (_speaker.isPlaying)
            _speaker.Stop();
        _speaker.clip = clip;
        _speaker.Play();
    }
}