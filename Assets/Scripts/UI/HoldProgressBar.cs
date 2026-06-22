using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

[RequireComponent(typeof(Image)) ]
public class HoldProgressBar : MonoBehaviour
{
    [SerializeField] private Color _endColor;
    private Image _progressImage;
    private Color originalColor;

    public System.Action OnHoldComplete;
    public System.Action OnHoldCancel;

    private Tween _tween;
    private float _curTime;
    private bool _isCanceled;
    // If true, the bar restarts automatically as soon as a hold completes.
    // The loot system (GarbageObject) uses this so the player can keep E held
    // and loot multiple items from the same prefab; one-shot interactions
    // (WaterFilter, HoleInFance) leave it at the default false.
    private bool _loop;

    private void Awake()
    {
        _progressImage = GetComponent<Image>();
        originalColor = _progressImage.color;
    }

    public void StartHold(float holdTime, bool loop = false)
    {
        _isCanceled = false;
        _curTime = holdTime;
        _loop = loop;
        _progressImage.color = originalColor;
        _progressImage.fillAmount = 0f;
        _tween?.Kill();
        _tween = _progressImage.DOFillAmount(1f, holdTime).OnComplete(CompleteHold);
        _progressImage.DOColor(_endColor, holdTime);
    }

    public void CancelHold()
    {
        if (!_progressImage) return;
        _isCanceled = true;
        _tween?.Kill();
        _progressImage.fillAmount = 0;
        OnHoldCancel?.Invoke();
    }

    private void CompleteHold()
    {
        _tween?.Kill();
        _progressImage.fillAmount = 0f;
        OnHoldComplete?.Invoke();
        // Auto-restart only when the caller asked for looped holds (looting).
        // Without this guard, holding E at the WaterFilter would loop forever.
        if (!_isCanceled && _loop)
            StartHold(_curTime, loop: true);
    }
}
