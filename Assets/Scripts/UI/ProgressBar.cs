using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    [SerializeField] private Image _bar;
    [SerializeField] private Text _count;
    private float _nominalWidth;
    public void SetAmount(float curren, float max)
    {
        // _bar and _count are serialized references to UI elements that live
        // in the scene. On scene reload (eg game complete -> restart), the
        // scene is unloaded and these references point to destroyed objects.
        // The PlayerState.Tic() coroutine keeps running across the reload
        // (Zenject singleton survives) and calls SetAmount on the destroyed
        // bar, which throws MissingReferenceException. The Unity == null
        // check is the right guard here - it returns true for destroyed
        // objects (Unity's operator == override handles the 'fake null' case).
        if (_bar == null) return;

        if(_nominalWidth == 0)
            _nominalWidth = _bar.rectTransform.rect.width;

        var amount = curren / max;
        _bar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _nominalWidth * amount);
    }
    public void SetAmountAndValue(float curren, float max)
    {
        SetAmount(curren, max);
        if (_count != null) _count.text = ((int)curren).ToString();
    }
    public void SetAmountCurAndMax(float curren, float max)
    {
        SetAmount(curren, max);
        if (_count != null) _count.text = (int)curren + "/" + (int)max;
    }
}
