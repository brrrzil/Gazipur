using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class Tooltipe : MonoBehaviour
{
    [SerializeField] private Text _text;
    private CanvasGroup _group;
    private Tween _tween;
    private void Start()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0;
    }
    public void Show(string text)
    {
        _text.text = text;
        _tween?.Kill();
        _tween = _group.DOFade(1, 0.5f);
    }
    public void Hide()
    {
        _tween?.Kill();
        _tween = _group.DOFade(0, 1f);
    }
}
