using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using Zenject;
using static EnumData;

[RequireComponent(typeof(Button))]
public class ButtonAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Button _button;
    private Vector3 _rotate;
    private Tween _tween;

    // Round 72: hover sound. Sounds is a scene-persistent service
    // (DontDestroyOnLoad in Sounds.Init), so the [Inject] is the same
    // instance in MainMenu and in GameScene and in pause/dialog panels.
    // The hover clip itself is bound by the user in
    // GameManager.prefab -> Sounds._uiSound (one UISoundData with
    // sound = UISound.buttonHover).
    [Inject] private Sounds _sounds;

    public void OnPointerEnter(PointerEventData eventData)
    {
        //_tween?.Kill();
        _button.transform.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutElastic);
        // Play the hover clip through the central UI audio source
        // so it goes through AudioMixer's Sound group and obeys the
        // SoundsVolume slider. Null-guard covers the editor 'play'
        // mode without DI context, and any prefab instantiated
        // outside a Zenject scene.
        if (_sounds != null)
            _sounds.UIPlay(UISound.buttonHover);
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