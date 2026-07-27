using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class ButtonAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Button _button;
    private Vector3 _rotate;
    private Tween _tween;

    public void OnPointerEnter(PointerEventData eventData)
    {
        //_tween?.Kill();
        _button.transform.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutElastic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tween?.Kill();
        _button.transform.DORotate(_rotate, 0.7f).SetEase(Ease.OutElastic); 
    }

    void Start()
    {
       _button = GetComponent<Button>();
       _rotate = transform.rotation.eulerAngles;
    }
}