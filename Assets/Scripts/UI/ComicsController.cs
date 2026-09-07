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
    [Tooltip("Button shown after the last slide. Click to dismiss the comics and invoke the finished event.")]
    [SerializeField] private Button _startButton;

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

    [Inject] private GameModeManager _modManager;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_startButton != null)
        {
            _startButton.gameObject.SetActive(false);
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (_showOnce && PlayerPrefs.GetInt(PREFS_KEY, 0) == 1)
        {
            _finished = true;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
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

        _onComicsFinished?.Invoke();
    }
}

public static class AudioSourceExtensions
{
    public static bool IsPlayingSafe(this AudioSource source)
    {
        return source != null && source.isPlaying;
    }
}
