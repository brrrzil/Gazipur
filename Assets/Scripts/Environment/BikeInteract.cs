using UnityEngine;

// Interactable for the bicycle models on the location. The
// inherited Outline component is enabled/disabled by
// InteractObject.Select(); leave the inherited _tooltipeText
// field empty to suppress the tooltip. Intearct() plays the
// bell once per E press via the local AudioSource (drag the
// bike bell AudioClip onto it, or assign an AudioSource that
// already has the bell clip).
public class BikeInteract : InteractObject
{
    [SerializeField] private AudioSource _bellSource;

    public override void Intearct(bool isDown)
    {
        if (!isDown) return;
        if (_bellSource != null) _bellSource.Play();
    }
}
