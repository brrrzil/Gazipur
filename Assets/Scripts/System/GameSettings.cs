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
    }
    private void ChangeMusicVolume(float value)
    {
        _sounds.ChangeMusicVolume(value);
    }
    private void ChangeSoundVolume(float value)
    {
        _sounds.ChangeSoundVolume(value);
    }
    private void Mute(bool isMute)
    {
        _sounds.Mute(isMute);
    }
}
