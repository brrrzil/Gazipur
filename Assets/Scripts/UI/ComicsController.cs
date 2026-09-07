using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    [Header("Events")]
    [Tooltip("Invoked when the player clicks the Start button. Use this to start the dialog that should follow the comics.")]
    [SerializeField] private UnityEvent _onComicsFinished;

    private CanvasGroup _canvasGroup;
    private int _currentSlide = -1;
    private bool _finished;
    private Coroutine _transitionRoutine;

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
            // Comics already shown on a previous run - skip.
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
        if (_autoAdvanceAfterVoice && _voiceSource != null && !_voiceSource.isPlaying
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
        // Fade out the currently visible slide.
        if (_currentSlide >= 0 && _currentSlide < _slides.Length && _slides[_currentSlide] != null)
        {
            var cgOut = GetOrAddCanvasGroup(_slides[_currentSlide]);
            for (float t = 0f; t < _fadeDuration; t += Time.deltaTime)
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
            for (float t = 0f; t < _fadeDuration; t += Time.deltaTime)
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
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        if (_voiceSource != null) _voiceSource.Stop();
        _onComicsFinished?.Invoke();
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
}
