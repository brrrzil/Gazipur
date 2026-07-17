using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Zenject;

public class GameSettings : MonoBehaviour
{
    [SerializeField] private Slider _musicVoloumeSlider;
    [SerializeField] private Slider _soundVoloumeSlider;
    [SerializeField] private Slider _voicesVolumeSlider;
    [SerializeField] private Slider _mouseSensSlider;
    [SerializeField] private Toggle _muteToggle;
    [Inject] SoundControl _sounds;
    public void Start()
    {
        // Lock framerate to 60. Disable VSync so targetFrameRate is honored.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        // (round 49, kept after round 50 cleanup) Fallback to
        // FindObjectOfType if Zenject's ProjectInstaller binding did not
        // inject a SoundControl. Costs nothing at runtime, but shields
        // the panel from a future DI regression where the injection
        // silently fails and the user is left with a settings panel
        // that does nothing.
        if (_sounds == null)
        {
            Debug.LogError("[GameSettings] Zenject did not inject SoundControl. Falling back to FindObjectOfType.");
            _sounds = FindObjectOfType<SoundControl>();
        }
        if (_sounds == null)
        {
            Debug.LogError("[GameSettings] SoundControl not found in scene. Sliders / Mute will not work.");
            return;
        }
        // Show the actual saved volume, including 0. The previous "==0 ? 1 : value"
        // pattern caused the slider to snap back to 1 after the user dragged it
        // to silence, because every time the panel opened the saved 0 got
        // remapped to 1.
        _musicVoloumeSlider.value = _sounds.MusicVolume;
        _soundVoloumeSlider.value = _sounds.SoundVolume;
        // (round 60) Voices slider. The actual GameObject the
        // SerializeField points to is in Canvas.prefab under a
        // GameObject named 'Voices' (the user added this in
        // commit 684c22d). The binding is scene-level: you drag
        // the slider from the Canvas hierarchy into this
        // SerializeField slot in the Inspector. The serialized
        // ref therefore lives on the SettingsPanel scene
        // instance, not inside SettingsPanel.prefab.
        // _voicesVolumeSlider is allowed to be null — if the
        // user has not yet wired it up, the code below silently
        // skips the Voices channel rather than NRE'ing the
        // whole settings panel (lesson from round 49: a partial
        // settings panel is more useful than no settings panel).
        if (_voicesVolumeSlider != null)
        {
            _voicesVolumeSlider.value = _sounds.VoicesVolume;
            _voicesVolumeSlider.onValueChanged.AddListener(ChangeVoicesVolume);
        }
        _muteToggle.isOn = _sounds.IsMute;
        _musicVoloumeSlider.onValueChanged.AddListener(ChangeMusicVolume);
        _soundVoloumeSlider.onValueChanged.AddListener(ChangeSoundVolume);
        _muteToggle.onValueChanged.AddListener(Mute);
        // (round 47) Force a one-shot sync in case the toggle setter
        // above (isOn = ...) fired onValueChanged before AddListener was
        // attached, leaving the mixer in the previous state.
        Mute(_muteToggle.isOn);
    }
    private void ChangeMusicVolume(float value)
    {
        if (_sounds == null) { return; }
        _sounds.ChangeMusicVolume(value);
    }
    private void ChangeSoundVolume(float value)
    {
        if (_sounds == null) { return; }
        _sounds.ChangeSoundVolume(value);
    }
    private void ChangeVoicesVolume(float value)
    {
        if (_sounds == null) { return; }
        _sounds.ChangeVoicesVolume(value);
    }
    private void Mute(bool isMute)
    {
        if (_sounds == null) { return; }
        _sounds.Mute(isMute);
    }
}
