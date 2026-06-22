using NaughtyAttributes;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using DG.Tweening;
using static EnumData;
public class DialogManager : MonoBehaviour
{
    public DialogType Dialog { get; private set; }
    [field: SerializeField] public CharacterRemarks Remarks { get; private set; }
    [SerializeField] private Text _questionText;
    [SerializeField] private Button[] _ansverButtons;

    [SerializeField] private DialogData[] _dialogs;

    [System.Serializable]
    public class DialogData
    {
        public DialogType dialogType;
        public DialogStructure iteration;
        public bool isOneTime;
        [HideInInspector] public bool isUsed;
    }
    [Inject] GameManager _manager;
    [Inject] GameModeManager _modManager;
    [Inject] Sounds _sounds;
    private AudioSource _speaker => _sounds.DialogSource;
    private AudioClip _curQuestClip;

    // Tracks the in-progress answer→question coroutine so we can cancel it if
    // the player (or a game-mode change) interrupts the chain.
    private Coroutine _voiceSequence;

    private void Start()
    {
        StartDialog(DialogType.motherStart);
        _modManager.onChangeMode += m =>
        {
            if (m == GameMode.outdors)
            {
                _curQuestClip = null;
                _speaker.Stop();
                if (_voiceSequence != null)
                {
                    StopCoroutine(_voiceSequence);
                    _voiceSequence = null;
                }
            }
        };
    }

    public bool StartDialog(DialogType dType)
    {
        // BUGFIX (M2): _dialogs.Where(...).ToArray()[0] would throw
        // IndexOutOfRangeException when the requested dialog type is missing.
        var matches = _dialogs.Where(i => i.dialogType == dType).ToArray();
        if (matches.Length == 0) return false;

        var dialog = matches[0];
        if (dialog.isUsed) return false;

        Dialog = dialog.dialogType;
        dialog.isUsed = dialog.isOneTime;
        SetIteration(dialog.iteration);
        _modManager.ChangeMode(GameMode.dialog);
        return true;
    }

    private void SetIteration(DialogStructure iteraton)
    {
        // If a previous answer→question sequence is still mid-flight, kill it
        // so the new question voice doesn't fight with a delayed answer voice.
        if (_voiceSequence != null)
        {
            StopCoroutine(_voiceSequence);
            _voiceSequence = null;
        }

        if (iteraton.QuestionVoice)
        {
            // Cut whatever is currently playing and start the new question
            // immediately. The previous DOTween-queue approach let a stale
            // voice play seconds later after _speaker.Stop() was called on
            // game mode change to outdors.
            if (_speaker.isPlaying)
                _speaker.Stop();
            _speaker.clip = iteraton.QuestionVoice;
            _speaker.Play();
            _curQuestClip = iteraton.QuestionVoice;
        }

        _questionText.text = iteraton.Question;
        for (int i = 0; i < _ansverButtons.Length; i++)
        {
            if (i >= iteraton.Answer.Length)
            {
                _ansverButtons[i].gameObject.SetActive(false);
                continue;
            }

            _ansverButtons[i].gameObject.SetActive(true);
            _ansverButtons[i].GetComponentInChildren<Text>().text = iteraton.Answer[i].answer;
            _ansverButtons[i].onClick.RemoveAllListeners();

            int idx = i;
            if (iteraton.Answer[i].newChain != null)
            {
                _ansverButtons[i].onClick.AddListener(() =>
                {
                    // BUGFIX (M1): previously the answer voice was started and
                    // then SetIteration was called immediately, which stops
                    // the speaker and plays the next question voice — cutting
                    // the answer voice mid-clip. Now we play the answer voice
                    // first, then call SetIteration only when it finishes.
                    var answerClip = iteraton.Answer[idx].answerVoice;
                    var nextChain = iteraton.Answer[idx].newChain;

                    if (_voiceSequence != null)
                        StopCoroutine(_voiceSequence);
                    _voiceSequence = StartCoroutine(PlayAnswerThenChain(answerClip, nextChain));
                });
            }
            else
            {
                _ansverButtons[i].onClick.AddListener(() =>
                {
                    // End-of-dialog branch. The mode change triggers
                    // _speaker.Stop() via the onChangeMode handler above, so
                    // we just need to play the answer voice after that.
                    _modManager.ChangeMode(GameMode.outdors);
                    if (_voiceSequence != null)
                        StopCoroutine(_voiceSequence);
                    if (iteraton.Answer[idx].answerVoice)
                    {
                        if (_speaker.isPlaying)
                            _speaker.Stop();
                        _speaker.clip = iteraton.Answer[idx].answerVoice;
                        _speaker.Play();
                    }
                });
            }

            if (iteraton.Answer[i].action)
            {
                _ansverButtons[i].onClick.AddListener(() => iteraton.Answer[idx].action.Action(_manager));
            }
        }
    }

    // Coroutine: play the answer voice to completion, then advance to the
    // next dialog chain. Cancellation is the caller's responsibility (see
    // the _voiceSequence checks in SetIteration and the mode handler).
    private IEnumerator PlayAnswerThenChain(AudioClip answerClip, DialogStructure nextChain)
    {
        if (answerClip)
        {
            if (_speaker.isPlaying)
                _speaker.Stop();
            _speaker.clip = answerClip;
            _speaker.Play();
            yield return new WaitForSeconds(answerClip.length);
        }

        _voiceSequence = null;
        if (nextChain != null)
            SetIteration(nextChain);
    }
}
