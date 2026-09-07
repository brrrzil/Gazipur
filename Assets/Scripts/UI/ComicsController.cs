using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ComicsController : MonoBehaviour
{
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

    [Header("Events")]
    [Tooltip("Invoked when the player clicks the Start button. Use this to start the dialog that should follow the comics.")]
    [SerializeField] private UnityEvent _onComicsFinished;

    private CanvasGroup _canvasGroup;
    private int _currentSlide = -1;
    private bool _finished;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_startButton != null)
        {
            _startButton.gameObject.SetActive(false);
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }

    private void Start()
    {
        ShowSlide(0);
    }

    private void Update()
    {
        if (_finished) return;
        if (_advanceOnClick && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            Advance();
        }
        if (_autoAdvanceAfterVoice && _voiceSource != null && !_voiceSource.isPlaying && _currentSlide >= 0 && _currentSlide < _slides.Length - 1)
        {
            Advance();
        }
    }

    public void Advance()
    {
        if (_currentSlide < _slides.Length - 1)
        {
            ShowSlide(_currentSlide + 1);
        }
        else
        {
            ShowStartButton();
        }
    }

    private void ShowSlide(int index)
    {
        if (index < 0 || index >= _slides.Length) return;
        for (int i = 0; i < _slides.Length; i++)
        {
            if (_slides[i] != null) _slides[i].SetActive(i == index);
        }
        _currentSlide = index;

        if (_voiceSource != null && _voiceClips != null && index < _voiceClips.Length && _voiceClips[index] != null)
        {
            _voiceSource.Stop();
            _voiceSource.clip = _voiceClips[index];
            _voiceSource.Play();
        }
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
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        if (_voiceSource != null) _voiceSource.Stop();
        _onComicsFinished?.Invoke();
    }
}
