using UnityEngine;

/// <summary>
/// Keeps the gameplay Canvas alive across scene reloads.
///
/// Why: GameScene's Canvas is a local object (not a prefab instance),
/// and many MonoBehaviours hold [SerializeField] refs to UI children
/// of that Canvas - DataManager._moneyCount, Inventory._picedItems,
/// ItemInfoPanel._useButton, etc. Single-mode scene reload destroys
/// the old Canvas and replaces it with a fresh one, leaving every
/// SerializeField UI ref pointing at a destroyed Unity object. UI
/// updates silently no-op because every 'if (_ref != null)' guard
/// now reads false.
///
/// Putting the Canvas in DDOL solves it at the source: the Canvas
/// survives the reload, every SerializeField ref stays valid, and
/// reloading just changes which scene is "active" while the UI tree
/// itself is persistent.
///
/// Lifecycle:
/// - AfterSceneLoad (MainMenu): no Canvas exists yet on MainMenu, so
///   we no-op. MainMenu's own menu Canvas lives only for the menu.
/// - AfterSceneLoad (GameScene): the new Canvas is the *real* gameplay
///   Canvas. If a previous run already parked one in DDOL (e.g. the
///   player hit Continue without ever returning to the main menu),
///   destroy the newcomer so we don't end up with two stacked Canvases.
/// - On any other scene load, if a Canvas exists in DDOL and a new one
///   appeared on the new scene, destroy the new one to keep the tree
///   canonical.
/// </summary>
public static class CanvasPersister
{
    private const string DDOLHostName = "[GameCanvas (DDOL)]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSingleCanvas()
    {
        // Skip on the menu scene - the MainMenu Canvas is a different
        // beast (full-screen menu UI, not the gameplay HUD). Persisting
        // it would just leak menu panels into the next GameScene load.
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.name.Contains("Game")) return;

        // Find the DDOL host, if any. The Canvas lives as a direct
        // child of the host so we never have to scan every DDOL object.
        var existingHost = GameObject.Find(DDOLHostName);

        // Find the Canvas on the freshly-loaded scene.
        Canvas sceneCanvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            // Skip Canvases that are already under our DDOL host (would
            // only happen if MainMenuScript somehow loaded twice without
            // our hook firing in between).
            if (existingHost != null && c.transform.root == existingHost.transform) continue;
            sceneCanvas = c;
            break;
        }

        if (existingHost == null)
        {
            // First GameScene load (or fresh launch). Move the scene
            // Canvas into DDOL by reparenting onto a host we create.
            if (sceneCanvas == null) return; // no Canvas on this scene
            var host = new GameObject(DDOLHostName);
            Object.DontDestroyOnLoad(host);
            sceneCanvas.transform.SetParent(host.transform, worldPositionStays: false);
        }
        else if (sceneCanvas != null)
        {
            // We already have a DDOL Canvas from a previous load. The
            // scene Canvas is a duplicate that came in with the new
            // GameScene; destroy it before it stacks on top of ours.
            Object.Destroy(sceneCanvas.gameObject);
        }
    }
}
