using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zenject;

[RequireComponent(typeof(CanvasGroup))]
public class ComicsController : MonoBehaviour
{
    private const string PREFS_KEY = "comics_opening_shown";

    [Header("Slides")]
    [Tooltip("Each slide is a child GameObject (Image/RawImage) shown one at a time. The first slide is shown on Start.")]
    [SerializeField] private GameObject[] _slides;
    [Tooltip("Audio source for voice-over playback. Optional - leave empty if no voice-over.")]
    [SerializeField] private AudioSource _voiceSource;
    [Tooltip("Voice-over clip for each slide. Index matches _slides. Optional - leave empty if no voice-over.")]
    [SerializeField] private AudioClip[] _voiceClips;
    [Tooltip("Maximum display time per slide in seconds (parallel array to _slides). After this time the slide auto-advances. Set to 0 (or leave the slot empty) to skip the auto-advance for that slide.")]
    [SerializeField] private float[] _slideDurations;
    [Tooltip("Button shown after the last slide. Click to dismiss the comics and invoke the finished event.")]
    [SerializeField] private Button _startButton;
    [Tooltip("Optional skip button shown from the start. Click to dismiss the comics immediately and skip to the finished event (same as clicking Start on the last slide).")]
    [SerializeField] private Button _skipButton;

    [Header("Behaviour")]
    [Tooltip("If true, advance to the next slide on left mouse click or Space. If false, only the button advances.")]
    [SerializeField] private bool _advanceOnClick = true;
    [Tooltip("If true, automatically advance after the voice-over finishes. Requires voice-over to be set.")]
    [SerializeField] private bool _autoAdvanceAfterVoice = false;
    [Tooltip("Fade in / fade out duration when switching slides (seconds).")]
    [SerializeField] private float _fadeDuration = 1f;
    [Tooltip("If true, the comics only plays once per game install (tracked via PlayerPrefs). If false, it plays every time.")]
    [SerializeField] private bool _showOnce = false;
    [Tooltip("If true, the game is paused (Time.timeScale = 0) and all audio is paused while the comics are showing. Use unscaledDeltaTime for UI animations in this case.")]
    [SerializeField] private bool _pauseGame = true;

    [Header("Events")]
    [Tooltip("Invoked when the player clicks the Start button. Use this to start the dialog that should follow the comics.")]
    [SerializeField] private UnityEvent _onComicsFinished;

    private CanvasGroup _canvasGroup;
    private int _currentSlide = -1;
    private bool _finished;
    private Coroutine _transitionRoutine;
    private float _savedTimeScale = 1f;
    private float _currentSlideTime;

    [Inject] private GameModeManager _modManager;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_startButton != null)
        {
            _startButton.gameObject.SetActive(false);
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }
        if (_skipButton != null)
        {
            _skipButton.gameObject.SetActive(true);
            _skipButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (_showOnce && PlayerPrefs.GetInt(PREFS_KEY, 0) == 1)
        {
            _finished = true;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            return;
        }

        // (round 102) Skip the opener whenever the user is loading a
        // Continue save. The cleaner design would be a comicsCompleted
        // flag on SaveData, but for installs that pre-date the Save
        // System (i.e. the tester's own save files) JsonUtility won't
        // populate the new field and would land on the default false,
        // which would replay the intro every Continue. Bracket the
        // decision purely on 'HasSave' - SaveSystem.DeleteSave wipes
        // the slot on New Game so this branch is only taken on
        // Continue, where the user clearly already played.
        if (SaveSystem.HasSave())
        {
            _finished = true;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            // Stamp the new field on the loaded blob so future loads
            // (when this SaveData schema is the only one that exists)
            // see it. We DON'T call SaveNow here - SaveBootstrap is
            // the one responsible for the post-load snapshot, and we
            // don't want comics to fire a Save mid-Awake.
            var data = SaveSystem.Load();
            if (data != null)
            {
                data.comicsCompleted = true;
                // Save once so this upgrade persists for the next
                // Continue test cycle. If the load-then-save race
                // collides with SaveBootstrap's own load, both end
                // with the same flag value, so a duplicate Save is
                // harmless.
                SaveSystem.Save(data);
            }
            return;
        }
    }

    private void Start()
    {
        if (_finished) return;

        if (_pauseGame)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        if (_modManager != null) _modManager.ChangeMode(EnumData.GameMode.comics);

        ShowSlideImmediate(0);
        if (_showOnce)
        {
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();
        }
    }

    private void Update()
    {
        if (_finished) return;
        if (_advanceOnClick)
        {
            bool advancePressed =
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
            if (advancePressed) Advance();
        }
        if (_autoAdvanceAfterVoice && _voiceSource != null && !_voiceSource.IsPlayingSafe()
            && _currentSlide >= 0 && _currentSlide < _slides.Length - 1
            && _transitionRoutine == null)
        {
            Advance();
        }
        // Auto-advance when the current slide's max display time has elapsed.
        // Uses unscaledDeltaTime so this works even when Time.timeScale = 0.
        if (_currentSlide >= 0 && _currentSlide < _slideDurations.Length
            && _slideDurations[_currentSlide] > 0f
            && _transitionRoutine == null)
        {
            _currentSlideTime += Time.unscaledDeltaTime;
            if (_currentSlideTime >= _slideDurations[_currentSlide])
                Advance();
        }
    }

    public void Advance()
    {
        if (_transitionRoutine != null) return;
        if (_currentSlide < _slides.Length - 1)
        {
            _transitionRoutine = StartCoroutine(TransitionToSlide(_currentSlide + 1));
        }
        else
        {
            ShowStartButton();
        }
    }

    private void ShowSlideImmediate(int index)
    {
        if (index < 0 || index >= _slides.Length) return;
        for (int i = 0; i < _slides.Length; i++)
        {
            if (_slides[i] == null) continue;
            bool active = i == index;
            _slides[i].SetActive(active);
            if (active)
            {
                var cg = GetOrAddCanvasGroup(_slides[i]);
                cg.alpha = 1f;
            }
        }
        _currentSlide = index;
        _currentSlideTime = 0f;

        if (_voiceSource != null && _voiceClips != null && index < _voiceClips.Length && _voiceClips[index] != null)
        {
            _voiceSource.Stop();
            _voiceSource.clip = _voiceClips[index];
            _voiceSource.Play();
        }
    }

    private IEnumerator TransitionToSlide(int newIndex)
    {
        // Use unscaled time so the fade works even when Time.timeScale = 0.
        float dt = _pauseGame ? Time.unscaledDeltaTime : Time.deltaTime;

        // Fade out the currently visible slide.
        if (_currentSlide >= 0 && _currentSlide < _slides.Length && _slides[_currentSlide] != null)
        {
            var cgOut = GetOrAddCanvasGroup(_slides[_currentSlide]);
            for (float t = 0f; t < _fadeDuration; t += dt)
            {
                cgOut.alpha = 1f - (t / _fadeDuration);
                yield return null;
            }
            cgOut.alpha = 0f;
            _slides[_currentSlide].SetActive(false);
        }

        // Switch + fade in.
        if (newIndex >= 0 && newIndex < _slides.Length && _slides[newIndex] != null)
        {
            _slides[newIndex].SetActive(true);
            var cgIn = GetOrAddCanvasGroup(_slides[newIndex]);
            cgIn.alpha = 0f;
            for (float t = 0f; t < _fadeDuration; t += dt)
            {
                cgIn.alpha = t / _fadeDuration;
                yield return null;
            }
            cgIn.alpha = 1f;
        }
        _currentSlide = newIndex;
        _currentSlideTime = 0f;

        if (_voiceSource != null && _voiceClips != null && newIndex < _voiceClips.Length && _voiceClips[newIndex] != null)
        {
            _voiceSource.Stop();
            _voiceSource.clip = _voiceClips[newIndex];
            _voiceSource.Play();
        }

        _transitionRoutine = null;
    }

    private void ShowStartButton()
    {
        if (_startButton != null) _startButton.gameObject.SetActive(true);
        if (_voiceSource != null) _voiceSource.Stop();
    }

    public void OnStartButtonClicked()
    {
        if (_finished) return;
        _finished = true;
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);

        if (_pauseGame)
        {
            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        if (_voiceSource != null) _voiceSource.Stop();

        // Switch to outdors first - the dialog's StartDialog will set the
        // mode to dialog after the dialog actually opens. This avoids a
        // flicker where the player is briefly in dialog mode with no dialog
        // UI yet (the dialog UI initialises after ChangeMode is called).
        if (_modManager != null) _modManager.ChangeMode(EnumData.GameMode.outdors);

        // (round 102) Mark the opening comics as completed in the save
        // blob so a Continue does not replay them. Stamps every save
        // from here on; New Game still wipes the whole slot via
        // MainMenuScript.OnNewGame so a fresh run sees the intro again.
        if (SaveSystem.HasSave())
        {
            var data = SaveSystem.Load();
            if (data != null)
            {
                data.comicsCompleted = true;
                SaveSystem.Save(data);
            }
        }

        _onComicsFinished?.Invoke();
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
}

public static class AudioSourceExtensions
{
    public static bool IsPlayingSafe(this AudioSource source)
    {
        return source != null && source.isPlaying;
    }
}
