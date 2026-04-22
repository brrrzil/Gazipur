using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    [SerializeField] private Image _bar;
    [SerializeField] private Text _count;
    private float _nominalWidth;
    public void SetAmount(float curren, float max)
    {
        if(_nominalWidth == 0)
            _nominalWidth = _bar.rectTransform.rect.width;

        var amount = curren / max;        
        _bar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _nominalWidth * amount);
    }
    public void SetAmountAndValue(float curren, float max)
    {
        SetAmount(curren, max);
        _count.text = ((int)curren).ToString();
    }
    public void SetAmountCurAndMax(float curren, float max)
    {
        SetAmount(curren, max);
        _count.text = (int)curren + "/" + (int)max;
    }
}
