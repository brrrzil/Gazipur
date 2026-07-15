using UnityEngine;
using Zenject;

public class ProjectInstaller : MonoInstaller
{
    [SerializeField] private SoundControl _soundControl;
    public override void InstallBindings()
    {
        // (round 48) Switch from FromInstance to FromComponentInNewPrefab.
        // The previous FromInstance used the prefab reference that is
        // serialized in the ProjectContext inspector, but the prefab was
        // never actually instantiated in the scene, so the bound
        // SoundControl stayed null and every [Inject] field on
        // GameSettings received null. The null reference then made
        // Mute() / ChangeMusicVolume() / ChangeSoundVolume() silently
        // NRE on every slider tick, while the mixer itself stayed at
        // its default 0 dB so the user heard full-volume audio
        // regardless of slider position or mute toggle.
        //
        // FromComponentInNewPrefab tells Zenject to instantiate the
        // assigned prefab the first time SoundControl is resolved, and
        // cache the result for the rest of the session (AsSingle). The
        // instantiated GameObject lives under the ProjectContext so
        // it survives scene loads.
        Container.Bind<SoundControl>().FromComponentInNewPrefab(_soundControl).AsSingle();
    }
}
