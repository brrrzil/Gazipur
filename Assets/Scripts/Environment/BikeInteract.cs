using UnityEngine;
using Zenject;
using static EnumData;

// Interactable for the bicycle models on the location. The
// inherited _tooltipeText (Inspector) sets the tooltip; the
// inherited Outline component is enabled/disabled by
// InteractObject.Select(). Intearct() plays the bell sound once
// per E press. The 'bikeBell' PlayerSound is bound to an
// AudioClip by the user in GameManager.prefab -> Sounds._playerSounds[].
public class BikeInteract : InteractObject
{
    [SerializeField] private PlayerSound _bellSound = PlayerSound.bikeBell;

    [Inject] private Sounds _sounds;

    public override void Intearct(bool isDown)
    {
        if (!isDown) return;
        _sounds.PlayerPlay(_bellSound, false);
    }
}
