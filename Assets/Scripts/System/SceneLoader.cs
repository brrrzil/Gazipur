using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

// Thin MonoBehaviour wrapper that the Canvas prefab's
// persistent onClick bindings call into (the onClick
// can only target a UnityEngine.Object method - either
// a static or an instance method on a serialized
// component reference, not an arbitrary class method
// from a non-Object source like Zenject's own
// ZenjectSceneLoader, which is a plain C# class with
// no UnityEngine.Object identity and therefore cannot
// be referenced from a UnityEvent onClick in the
// Inspector). The wrapper delegates to the injected
// ZenjectSceneLoader, which is the
// Zenject-aware scene loader that calls
// ProjectKernel.ForceUnloadAllScenes() before
// SceneManager.LoadScene(), which is the only way
// to avoid the 'SceneContextRegistry.Add Assert hit
// (Scene is already added)' exception when reloading
// the same scene that contains a SceneContext.
//
// Why Zenject-aware loading is required here
// (and why the naive SceneManager.LoadScene
// path fails):
//
// Zenject's SceneContextRegistry stores a
// Dictionary<Scene, SceneContext> for all
// loaded scenes. Each SceneContext, on Awake,
// runs InitializableManager.Initialize(), which
// dispatches SceneContextRegistryAdderAndRemover
// .Initialize() and adds the SceneContext to the
// registry. The corresponding Dispose() call
// removes it. The registry enforces
// 'one SceneContext per scene' - the Add path
// has Assert.That(!_map.ContainsKey(scene))
// before _map.Add(...), and that assert is what
// the user is seeing in the round 86 restart
// flow.
//
// When the user clicks the 'Try Again' button in
// the DiePanel (or the 'Try Again' button in the
// WinPanel), the Canvas prefab's onClick fires
// SceneLoader.LoadScene(int scene) with
// scene=1 (GameScene build index). With the
// previous naive implementation
// (SceneManager.LoadScene(1, Single)), Unity
// would unload the current GameScene and load
// a fresh GameScene in the same frame. The
// 'unload + load' in LoadSceneMode.Single is
// not atomic from the MonoBehaviour lifecycle
// perspective: the old GameScene's GameObject
// hierarchy is torn down (and its SceneContext
// .Dispose() runs) but the order of OnDestroy
// and the registry Remove is not guaranteed
// relative to the new scene's Awake + Add
// call. If the new GameScene's Awake fires
// before the old GameScene's Dispose
// completes (or in the same frame, with the
// old SceneContext's Dispose scheduled but
// not yet executed), the registry sees
// 'Add(scene=GameScene)' for a scene it still
// has a live entry for, and the assert hits.
//
// ZenjectSceneLoader.LoadScene calls
// PrepareForLoadScene first, which for
// LoadSceneMode.Single calls
// _projectKernel.ForceUnloadAllScenes(). That
// method runs a synchronous loop that
// SceneManager.UnloadSceneAsync on every
// currently loaded scene and yields until
// the unload is done, then sets
// SceneContext.ParentContainers = null and
// SceneContext.ExtraBindingsInstallMethod,
// and only then calls SceneManager.LoadScene.
// Because the unload is forced to complete
// before the load, the old SceneContext
// has run its Dispose / removed its entry
// from the registry, and the new scene's
// Awake / Add sees an empty slot for its
// scene and succeeds.
//
// Round 86 vs round 27 vs round 87:
//
// Round 27 (commit d9cb40c) fixed this same
// assert hit by going in the opposite
// direction: it changed the TryAgainButton
// m_IntArgument from 1 to 0 (MainMenu) so
// the button goes to a different scene
// entirely, sidestepping the
// same-scene-reload conflict. That worked
// but the user wanted the DiePanel restart
// to actually restart the gameplay scene
// (not bounce to the main menu), so the
// round 86 commit flipped it back to 1.
// That re-exposed the assert.
//
// This commit is the 'round 87' fix: keep
// the round 86 m_IntArgument = 1
// (GameScene build index, so the button
// targets the gameplay scene), but route
// the actual load through
// ZenjectSceneLoader so the unload/load
// ordering is enforced by Zenject and the
// SceneContextRegistry is guaranteed to
// have the old entry removed before the
// new one is added.
//
// Why a MonoBehaviour wrapper at all
// (instead of switching the onClick to
// call a static method somewhere):
//
// Unity's persistent onClick calls must
// target a method on a UnityEngine.Object
// (the Inspector UI lists only the
// MonoBehaviour components that are
// serialized on the same GameObject as
// the Button - or on a reference dragged
// in the Inspector). Zenject's
// ZenjectSceneLoader is a plain C# class
// (no UnityEngine.Object identity), so
// it cannot be the onClick target
// directly. The SceneLoader MonoBehaviour
// is the bridge: the Button onClick
// references a SceneLoader instance (one
// is placed on DiePanel, one on WinPanel
// in the Canvas prefab), and the
// MonoBehaviour delegates to the injected
// ZenjectSceneLoader. The Injection
// itself happens because both DiePanel
// and WinPanel are loaded as part of
// GameScene, which has a SceneContext
// (the GameManager prefab in the scene
// root) - the SceneLoader MonoBehaviour
// is under the same root as that
// SceneContext, so Zenject's
// InitialComponentsInjecter walks the
// hierarchy at install time and injects
// ZenjectSceneLoader into the
// SceneLoader's [Inject] field.
//
// SceneContextRegistry (the dict that
// errors out) is bound AsSingle in
// ProjectContext.InstallBindings
// (_container.Bind<SceneContextRegistry>()
// .AsSingle()) and re-bound AsSingle
// in SceneContext.InstallBindings
// (the per-scene container). The
// ZenjectSceneLoader is also bound
// AsSingle in both ProjectContext and
// SceneContext, so a [Inject] on the
// MonoBehaviour resolves to the
// scene-scoped instance (which has the
// scene's _sceneContainer reference
// and the project kernel reference
// needed for ForceUnloadAllScenes).
public class SceneLoader : MonoBehaviour
{
    [Inject] private ZenjectSceneLoader _zenjectSceneLoader;

    public void LoadScene(int scene)
    {
        // Delegate to ZenjectSceneLoader so
        // the SceneContext from the
        // currently-active GameScene is
        // fully torn down (Dispose +
        // registry Remove) before the new
        // GameScene's Awake tries to Add
        // itself to the same key. Without
        // this delegation the same-scene
        // reload in LoadSceneMode.Single
        // produces the 'SceneContextRegistry
        // .Add Assert hit' error that the
        // user reported in round 86.
        //
        // LoadSceneMode.Single is the
        // default in ZenjectSceneLoader
        // .LoadScene (the enum's default
        // value), and the user wants
        // 'start the gameplay scene fresh
        // from this button', which is the
        // Single-mode semantics
        // (unload everything currently
        // loaded, then load the new one).
        //
        // The containerMode argument is
        // also defaulted to
        // LoadSceneRelationship.None
        // (also the enum's default),
        // which is what
        // PrepareForLoadScene requires
        // for LoadSceneMode.Single (it
        // asserts the two are equal). The
        // extraBindings / extraBindingsLate
        // delegates are left null - the
        // button does not need to inject
        // anything extra on the new
        // scene.
        //
        // Null-guard on _zenjectSceneLoader
        // for the case where the
        // SceneLoader MonoBehaviour is
        // somehow on a GameObject that is
        // not under a SceneContext (e.g.
        // placed in MainMenu's Canvas,
        // which is also a Canvas.prefab
        // instance but the MainMenu scene
        // has its own SceneContext on the
        // 'Installer' GameObject - so the
        // injection does happen, but the
        // guard costs nothing and
        // prevents an NRE in the
        // edge case where a future
        // Canvas placement drops the
        // injection chain). If the
        // injection did not happen, fall
        // back to the raw
        // SceneManager.LoadScene path
        // (same behaviour as before
        // round 87 - the user at least
        // gets the scene change, even
        // if it can hit the Zenject
        // assert).
        if (_zenjectSceneLoader != null)
        {
            _zenjectSceneLoader.LoadScene(scene, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(scene);
        }
    }
}
