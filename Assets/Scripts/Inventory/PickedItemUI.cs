using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class PickedItemUI : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Text _itemCountText;
    [SerializeField, Range(0,1f)] private float _defoultAlpha = 0.7f;

    private CanvasGroup _group;
    private Tween _tween;
    public void Show(ItemData item, int count)
    {        
        _tween?.Kill();
        _group ??= GetComponent<CanvasGroup>();
        _itemCountText.text = count.ToString();
        _itemIcon.sprite = item.Icon;
        gameObject.SetActive(true);
        _group.alpha = 0.7f;
        _tween = _group.DOFade(0, 0.5f).SetDelay(2).OnComplete(() => gameObject.SetActive(false));
    }
}
