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

    // Round 77: the hover AudioClip is the only public binding on
    // the button. No [Inject] on this class any more.
    //
    // Round 72 added [Inject] private Sounds _sounds; to call
    // _sounds.UIPlay(UISound.buttonHover). The dependency on
    // Sounds via Zenject was required to play the hover through
    // the central UI audio table. In this project that pipeline
    // is not wired up:
    //   - GameInstaller._sounds and MenuInstaller._sounds are
    //     both null in the saved prefabs.
    //   - SoundManager.prefab._uiSound is [] in every commit.
    //   - SoundManager.prefab is not instantiated in either
    //     scene.
    // So [Inject] private Sounds _sounds was a Zenject-
    // required dependency that resolved to null at every
    // call site. Keeping the [Inject] in the file even with a
    // null-guard at the call site is still a problem: Zenject
    // resolves dependencies eagerly, and an [Inject] on a
    // MonoBehaviour that lives in a scene with a
    // SceneContext (every MainMenu button has a SceneContext)
    // triggers a full DI graph walk. If the binding is missing,
    // Zenject throws ZenjectException at scene start BEFORE
    // the null-guard can run. Round 76's user console shows
    // exactly this: 'ZenjectException: Unable to resolve
    // 'Sounds' while building object with type
    // 'ButtonAnimation''.
    //
    // The audio path used in round 76 (UIAudio.Play, a static
    // helper) does not need Sounds at all. So the [Inject] is
    // removed in this commit, and the 'using Zenject;' /
    // 'using static EnumData;' lines that only existed to
    // support the dead Sounds path are also removed. Zenject
    // no longer visits ButtonAnimation, and the volume
    // sliders in MenuAudioManager (which were collateral
    // damage in the user's console: NRE on line 26 because
    // the resolution crash happened on a different thread)
    // are also unblocked. MenuAudioManager itself still has
    // its own NRE if the user has not bound the music/sound
    // slider and toggle fields in the editor - that is a
    // separate inspector-side issue, not a ButtonAnimation
    // regression and not addressed in this commit.
    [SerializeField] private AudioClip _hoverSound;

    public void OnPointerEnter(PointerEventData eventData)
    {
        //_tween?.Kill();
        _button.transform.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutElastic);

        // Round 76: play the hover through UIAudio.Play,
        // a static helper that lazily creates ONE
        // AudioSource on first call and routes it
        // through the AudioMixer 'Sound' group. This
        // replaces three earlier attempts that all
        // had problems:
        //
        //   - Round 72: _sounds.UIPlay(UISound
        //     .buttonHover) - dead in this project
        //     because GameInstaller._sounds is null
        //     and SoundManager.prefab._uiSound is [].
        //   - Round 74: AudioSource.PlayClipAtPoint -
        //     created a fresh throwaway GameObject
        //     per hover and did not route through
        //     the AudioMixer.
        //   - Round 75: UIAudio.Instance.Play - the
        //     UIAudio MonoBehaviour was correct in
        //     principle but required the user to
        //     add the component to a GameObject in
        //     the scene; without that, Awake never
        //     fired and Instance stayed null.
        //
        // The static UIAudio.Play needs no scene-side
        // component - it creates its own
        // DontDestroyOnLoad GameObject on the first
        // hover and reuses it forever after. The
        // clip itself is still per-button
        // ([SerializeField] AudioClip _hoverSound);
        // the user assigns the same AudioClip to
        // every ButtonAnimation they want to be
        // audible on hover.
        if (_hoverSound != null)
            UIAudio.Play(_hoverSound);
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