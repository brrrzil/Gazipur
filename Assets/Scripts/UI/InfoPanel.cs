using Assets.SimpleLocalization.Scripts;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class InfoPanel : MonoBehaviour
{
    public LocalizedText[] Texts => _descriptions; 
    [SerializeField] private CanvasGroup _cGroup;
    [SerializeField] float _showDuration;
    [SerializeField, Range(0,1)] float _alpha;
    [SerializeField] private Image _image;
    [SerializeField] private LocalizedText[] _descriptions;
      
    private Tween _tween;
    private void Start()
    {
        _image ??= GetComponent<Image>();
    }
    public void Show(string[] text, Vector2 position, Sprite _back)
    {
        _image ??= GetComponent<Image>();
        _image.sprite = _back;
        Show(text, position);        
    }
    public void Show(string[] text, Vector2 position)
    {
        SetData(text);
        Show(position);      
    }
    public void Show(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        Show();
    }
    public void Show(string[] text)
    {
        SetData(text);
        Show();
    }
    public void Show(string[] text, Sprite icon)
    {
        Show(text);
        _image.sprite = icon;
    }
    public void Show()
    {        
        gameObject.SetActive(true);
        _tween?.Kill(); 
        _tween = _cGroup.DOFade(_alpha, _showDuration);

    }
    public void Hide()
    {
        _tween?.Kill();
        _tween = _cGroup.DOFade(0, _showDuration).OnComplete(() => gameObject.SetActive(false));
    }
    public void SetData(string[] text)
    {
        for (int i = 0; i < _descriptions.Length; i++)
        {
            if (i <= text.Length - 1)
            {
                _descriptions[i].gameObject.SetActive(true);
                _descriptions[i].Text = text[i];
                continue;
            }
            _descriptions[i].gameObject.SetActive(false);

        }
    }
}
