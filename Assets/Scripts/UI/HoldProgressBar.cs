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
    private void Awake()
    {
        _progressImage = GetComponent<Image>();
        originalColor = _progressImage.color;
    }


    public void StartHold(float holdTime)
    {
        _isCanceled = false;
        _curTime = holdTime;
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
        // BUGFIX: previously this method called StartHold(_curTime) at the end,
        // which restarted the hold cycle as soon as it completed. While the
        // player kept E pressed (e.g. at the WaterFilter), this produced an
        // infinite loop — Finish() would fire over and over, draining sound
        // and consuming resources. Removed the auto-restart; callers that
        // need a multi-step hold must drive it themselves.
        _tween?.Kill();
        _progressImage.fillAmount = 0f;
        OnHoldComplete?.Invoke();
    }
}