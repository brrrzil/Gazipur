using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Zenject;

public class GameSettings : MonoBehaviour
{
    [SerializeField] private Slider _musicVoloumeSlider;
    [SerializeField] private Slider _soundVoloumeSlider;
    [SerializeField] private Slider _mouseSensSlider;
    [SerializeField] private Toggle _muteToggle;
    [Inject] SoundControl _sounds;
    public void Start()
    {
        // Lock framerate to 60. Disable VSync so targetFrameRate is honored.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        // (round 49) Fallback: if Zenject's ProjectInstaller binding did not
        // inject a SoundControl (e.g. the FromComponentInNewPrefab change
        // did not take effect, or the prefab field is missing), fall back
        // to FindObjectOfType so the panel still works instead of silently
        // doing nothing. LogError is loud so the missing injection is
        // visible in the Console even when other warnings are present.
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
        _muteToggle.isOn = _sounds.IsMute;
        _musicVoloumeSlider.onValueChanged.AddListener(ChangeMusicVolume);
        _soundVoloumeSlider.onValueChanged.AddListener(ChangeSoundVolume);
        _muteToggle.onValueChanged.AddListener(Mute);
        // (round 47) Force a one-shot sync in case the toggle setter
        // above (isOn = ...) fired onValueChanged before AddListener was
        // attached, leaving the mixer in the previous state.
        Mute(_muteToggle.isOn);
        Debug.Log($"[GameSettings] Start OK. Music={_sounds.MusicVolume:F2} SFX={_sounds.SoundVolume:F2} Mute={_sounds.IsMute}");
    }
    private void ChangeMusicVolume(float value)
    {
        if (_sounds == null) { Debug.LogError("[GameSettings] _sounds NULL in ChangeMusicVolume"); return; }
        Debug.Log($"[GameSettings] ChangeMusicVolume {value:F2}");
        _sounds.ChangeMusicVolume(value);
    }
    private void ChangeSoundVolume(float value)
    {
        if (_sounds == null) { Debug.LogError("[GameSettings] _sounds NULL in ChangeSoundVolume"); return; }
        Debug.Log($"[GameSettings] ChangeSoundVolume {value:F2}");
        _sounds.ChangeSoundVolume(value);
    }
    private void Mute(bool isMute)
    {
        if (_sounds == null) { Debug.LogError("[GameSettings] _sounds NULL in Mute"); return; }
        Debug.Log($"[GameSettings] Mute {isMute}");
        _sounds.Mute(isMute);
    }
}
