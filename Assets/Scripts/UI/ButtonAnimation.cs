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

    // Round 74: the hover sound is bound directly on the button itself
    // (per-instance AudioClip) instead of going through Sounds._uiSound.
    //
    // The previous design (round 72) tried to play the hover via
    // Sounds.UIPlay(UISound.buttonHover), which is a lookup into the
    // central Sounds._uiSound table. That table turned out to be
    // empty in this project: SoundManager.prefab has _uiSound: [],
    // GameManager.prefab has no Sounds component at all, and the
    // GameInstaller._sounds reference (the one that was supposed to
    // hand a Sounds instance to Zenject's Bind<Sounds>) is also null
    // in the saved prefab. So the [Inject] private Sounds _sounds
    // resolved to null, the if-guard caught it, and the hover clip
    // was never played. The Sounds service exists in the scene as a
    // dangling-prefab MonoBehaviour but is not wired into Zenject and
    // has no _uiSound rows to look up.
    //
    // To make the hover actually play without requiring the user to
    // rewire every installer + every prefab, the AudioClip now lives
    // on the ButtonAnimation itself. The user assigns ONE clip in the
    // inspector and it plays for every button that has a
    // ButtonAnimation component.
    //
    // We try Sounds first (so the existing central pipeline still
    // works if the user later wires it up) and fall back to
    // PlayClipAtPoint on the button's own transform if Sounds is
    // null. PlayClipAtPoint creates a temporary AudioSource on a
    // throwaway GameObject that lives until the clip finishes; the
    // clip routes to the default AudioListener, NOT through the
    // project's AudioMixer, so the SoundsVolume slider does not
    // affect it. That is a deliberate trade-off here - the previous
    // Sounds-based path did not play at all because of the empty
    // _uiSound table, so a working un-routed clip is strictly better
    // than a non-working routed one. If the user later wants the
    // hover to go through the mixer, they can (a) wire a Sounds
    // instance into GameInstaller._sounds, (b) add a row to that
    // Sounds._uiSound for buttonHover, and (c) the lookup at the
    // top of OnPointerEnter will then take over and the AudioClip
    // field becomes a fallback.
    [SerializeField] private AudioClip _hoverSound;
    [Inject] private Sounds _sounds;

    public void OnPointerEnter(PointerEventData eventData)
    {
        //_tween?.Kill();
        _button.transform.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutElastic);

        // Try the central Sounds service first. _sounds may be null
        // in scenes where GameInstaller._sounds was not bound in the
        // inspector; in that case the if-guard falls through to the
        // per-button AudioClip fallback below.
        if (_sounds != null)
        {
            _sounds.UIPlay(UISound.buttonHover);
            return;
        }

        // Per-button fallback. PlayClipAtPoint spawns a one-shot
        // AudioSource at the button's world position and disposes
        // it when the clip ends. Works without any AudioMixer
        // routing, which is fine for a UI click feedback sound.
        if (_hoverSound != null)
            AudioSource.PlayClipAtPoint(_hoverSound, transform.position);
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