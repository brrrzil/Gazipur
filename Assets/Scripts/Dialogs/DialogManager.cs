using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using DG.Tweening;
using static EnumData;
public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance { get; private set; }

    public DialogType Dialog { get; private set; }
    [field: SerializeField] public CharacterRemarks Remarks { get; private set; }
    [SerializeField] private Text _questionText;
    [SerializeField] private Button[] _ansverButtons;

    [SerializeField] private DialogData[] _dialogs;

    /// <summary>(SaveSystem) Read-only view onto the dialog list so
    /// GamePersistence.Collect can iterate the flags without exposing
    /// the serialized field directly.</summary>
    public System.Collections.Generic.IReadOnlyList<DialogData> AllDialogs => _dialogs;

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

    /// <summary>(SaveSystem) Mark the currently-running dialog as completed
    /// by setting its <see cref="DialogData.isUsed"/> flag. Called from
    /// SetIteration's end-of-dialog branch when the player picks a terminal
    /// answer. Persists via GamePersistence.SaveNow() which reads the
    /// updated flags on the next collect.</summary>
    public void MarkCurrentDialogUsed()
    {
        var matches = _dialogs.Where(d => d.dialogType == Dialog).ToArray();
        if (matches.Length == 0) return;
        matches[0].isUsed = matches[0].isOneTime;
    }

    /// <summary>(SaveSystem) Apply a list of completed dialog types loaded
    /// from SaveData. Sets each matching DialogData.isUsed = isOneTime so
    /// the player doesn't see the same dialog again after Continue.</summary>
    public void MarkDialogsUsedFromSave(IEnumerable<DialogType> completed)
    {
        if (completed == null) return;
        var byType = _dialogs.ToDictionary(d => d.dialogType);
        foreach (var dt in completed)
        {
            if (byType.TryGetValue(dt, out var data))
                data.isUsed = data.isOneTime;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // The opening motherStart dialog used to fire here directly, but
        // the game now opens with a comics sequence (a few slides + a
        // 'Start' button) before the dialog. Wire the ComicsController's
        // _onComicsFinished UnityEvent to this method's wrapper in the
        // Editor so the dialog starts when the player dismisses the comics.
        _modManager.onChangeMode += m =>
        {
            if (m == GameMode.outdors)
            {
                _curQuestClip = null;
                if (_speaker != null) _speaker.Stop();
                if (_voiceSequence != null)
                {
                    StopCoroutine(_voiceSequence);
                    _voiceSequence = null;
                }
            }
        };
    }

    public void StartOpeningDialog()
    {
        StartDialog(DialogType.motherStart);
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
        // (SaveSystem) BUGFIX: previously `dialog.isUsed = dialog.isOneTime;`
        // was set HERE, before the player walked through the dialog. That
        // meant an unfinished dialog got persisted on save, and on reload
        // the player could not re-trigger it. Move the flag flip into the
        // end-of-dialog branch in SetIteration so we only record finished
        // conversations.
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
            if (_speaker != null)
            {
                if (_speaker.isPlaying)
                    _speaker.Stop();
                _speaker.clip = iteraton.QuestionVoice;
                _speaker.Play();
            }
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
            // BUGFIX (round 16): always re-enable buttons when a new
            // question is shown. The click handler disables them while the
            // answer voice plays; SetIteration is the single source of
            // truth for "buttons are ready for input".
            _ansverButtons[i].interactable = true;
            _ansverButtons[i].GetComponentInChildren<Text>().text = iteraton.Answer[i].answer;
            _ansverButtons[i].onClick.RemoveAllListeners();

            int idx = i;
            if (iteraton.Answer[i].newChain != null)
            {
                _ansverButtons[i].onClick.AddListener(() =>
                {
                    // Unity null-check: if the player finishes the dialog
                    // and the scene unloads (New Game / Continue reload)
                    // before this coroutine ends, `this` may be a
                    // destroyed MonoBehaviour. Bail instead of throwing.
                    if (this == null) return;
                    // M1 (re-applied in round 11): play the answer voice to
                    // completion before advancing to the next question.
                    // Without this, the answer voice starts and is immediately
                    // cut off by SetIteration's Stop+Play of the new question
                    // voice — the user heard the NPC questions but not the
                    // protagonist's answers.
                    //
                    // BUGFIX (round 16): also disable every answer button
                    // while the answer voice plays, so the player can't
                    // spam-click and re-trigger the chain. SetIteration
                    // re-enables them when the next question is shown.
                    DisableAnswerButtons();
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
                    // Same scene-unload guard as the newChain branch above.
                    if (this == null) return;
                    // End-of-dialog branch. The mode change triggers
                    // _speaker.Stop() via the onChangeMode handler above, so
                    // we just need to play the answer voice after that.
                    //
                    // (SaveSystem) Mark the dialog as completed only here,
                    // after the player has chosen a terminal answer. If they
                    // quit mid-conversation the next launch replays from the
                    // start.
                    MarkCurrentDialogUsed();
                    GamePersistence.SaveNow();

                    _modManager.ChangeMode(GameMode.outdors);
                    if (_voiceSequence != null)
                        StopCoroutine(_voiceSequence);
                    if (iteraton.Answer[idx].answerVoice)
                    {
                        if (_speaker != null)
                        {
                            if (_speaker.isPlaying)
                                _speaker.Stop();
                            _speaker.clip = iteraton.Answer[idx].answerVoice;
                            _speaker.Play();
                        }
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
        if (answerClip && _speaker != null)
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

    // BUGFIX (round 16): disable every answer button while the protagonist's
    // answer voice is playing. SetIteration re-enables them when the next
    // question is shown, so this is just a temporary lock-out for the
    // duration of the voice playback.
    private void DisableAnswerButtons()
    {
        if (_ansverButtons == null) return;
        for (int i = 0; i < _ansverButtons.Length; i++)
        {
            if (_ansverButtons[i] != null)
                _ansverButtons[i].interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // Stop the in-flight answer-voice coroutine so a scene reload
        // doesn't leave _voiceSequence holding a reference to a
        // destroyed DialogManager.
        if (_voiceSequence != null)
        {
            StopCoroutine(_voiceSequence);
            _voiceSequence = null;
        }
    }
}
