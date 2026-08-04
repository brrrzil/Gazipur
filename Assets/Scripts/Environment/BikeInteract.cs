using UnityEngine;

// Interactable for the bicycle models on the location. The
// AudioSource is auto-added by RequireComponent; drag the bell
// AudioClip onto the bellClip field in the Inspector. The
// inherited Outline component is enabled/disabled by
// InteractObject.Select(); leave the inherited _tooltipeText
// field empty to suppress the tooltip. Intearct() plays the
// bell once per E press via PlayOneShot, so rapid E presses
// stack rather than cut each other off.
[RequireComponent(typeof(AudioSource))]
public class BikeInteract : InteractObject
{
    public AudioClip bellClip;

    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
    }

    public override void Intearct(bool isDown)
    {
        if (!isDown) return;
        if (_source != null && bellClip != null) _source.PlayOneShot(bellClip);
    }
}
