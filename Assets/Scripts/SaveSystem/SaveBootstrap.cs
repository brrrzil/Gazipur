using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-bootstrapped save/load entry point. Mirrors the pattern
/// FogController/FpsCounter use: a [RuntimeInitializeOnLoadMethod]
/// creates a hidden GameObject after scene load; that GameObject's
/// Start calls <see cref="GamePersistence.LoadIntoGame"/> with the
/// saved blob if one exists.
///
/// No scene wiring needed. If the player drops a real SaveBootstrap
/// MonoBehaviour into a scene, that one takes over and the
/// auto-bootstrapped instance bails.
///
/// Load timing: AutoCreate runs on the first scene load (MainMenu),
/// so an instance is already alive by the time the player hits
/// Continue and SceneManager.LoadScene moves us to GameScene. We
/// only want LoadIntoGame to run once the GameScene systems
/// (DataManager, Inventory, FogController, DialogManager) actually
/// exist - those are scene-context Zenject singletons that come up
/// with the scene. Use OnSceneLoaded to schedule the load on the
/// GameScene, not Start of the MainMenu.
///
/// Position is applied in LateUpdate so the player teleport
/// doesn't fight cell initialisation and the CharacterController /
/// Cinemachine have had a frame to wake up.
///
/// New Game flow: SaveSystem.DeleteSave() wipes the slot, so when we
/// enter GameScene HasSave() is false and LoadIntoGame is a no-op.
/// DataManager.Start sets _startMoney, FogController.Awake reads
/// RenderSettings.fogDensity, etc., giving authored scene defaults.
/// </summary>
public class SaveBootstrap : MonoBehaviour
{
    private static bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        // Don't double-create if the user has placed a SaveBootstrap
        // GameObject in the scene (FindFirstObjectByType also finds
        // disabled objects, which is what we want).
        if (FindFirstObjectByType<SaveBootstrap>() != null) return;
        var go = new GameObject("[SaveBootstrap]");
        DontDestroyOnLoad(go);
        go.AddComponent<SaveBootstrap>();

        // Subscribe to scene loads AND unloads. The static AfterSceneLoad
        // callback above only fires once per app session, which is not
        // enough for Continue-driven scene reload.
        if (!_subscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _subscribed = true;
        }
    }

    // (fix/input-after-continue) Cleanup runs on scene UNLOAD, not scene
    // load. Sounds.DontDestroyOnLoad(transform.root.gameObject) hoists
    // the entire GameManager hierarchy into DDOL during the Zenject
    // injection of the new GameScene - which happens *before*
    // sceneLoaded fires. By the time OnSceneLoaded runs and our previous
    // attempt called DestroyStaleGameManagerFromDDOL, the only DDOL
    // root with a SceneContext was the *freshly-instantiated* one, so
    // we ended up destroying the brand-new GameManager and left the
    // actual stale DDOL copy alone - the opposite of what we wanted.
    //
    // sceneUnloaded fires after the old scene's GameObjects are gone
    // but before the new scene's Awake/Start runs, which is exactly the
    // window where "only the stale DDOL GameManager exists, no new one
    // yet" is true. Destroy it there.
    private static void OnSceneUnloaded(Scene scene)
    {
        DestroyStaleGameManagerFromDDOL();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only restore on the GameScene. MainMenu has no PlayerState /
        // Inventory / FogController, so a Start() there would just
        // do nothing useful. Skipping it also keeps the player from
        // fighting inventory state when they're just sitting on the menu.
        if (!scene.name.Contains("Game")) return;

        // The previous SaveBootstrap (created by AutoCreate on MainMenu
        // or by an earlier scene load) already ran its Start against
        // whatever systems existed at that time - usually nothing if
        // we were on MainMenu. Destroy it so its Start can't fire
        // again, and create a fresh one whose Start runs against the
        // freshly-injected GameScene systems (DataManager, Inventory,
        // FogController, DialogManager).
        var existing = FindObjectsByType<SaveBootstrap>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null) UnityEngine.Object.Destroy(existing[i].gameObject);
        }

        var go = new GameObject("[SaveBootstrap]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SaveBootstrap>();
    }

    // (fix/input-after-continue) Helper: find the GameManager root that
    // Sounds hoisted into DDOL on the previous scene load and destroy it
    // before the new GameScene tries to instantiate a duplicate. We do
    // this by walking the DontDestroyOnLoad scene's root GameObjects and
    // matching the one that contains a SceneContext (the GameManager
    // root has a Zenject SceneContext child; the auto-bootstrapped
    // [SaveBootstrap] / [Sounds (DDOL)] hosts from other systems do not).
    private static void DestroyStaleGameManagerFromDDOL()
    {
        var ddolScene = SceneManager.GetSceneByName("DontDestroyOnLoad");
        if (!ddolScene.IsValid() || !ddolScene.isLoaded) return;
        foreach (var root in ddolScene.GetRootGameObjects())
        {
            if (root == null) continue;
            // The GameManager hierarchy contains a SceneContext component
            // somewhere down the tree. If we find one, this root is the
            // stale GameManager and we tear it down.
            var ctx = root.GetComponentInChildren<Zenject.SceneContext>(true);
            if (ctx != null)
            {
                Debug.Log($"[SaveBootstrap] Destroying stale DDOL GameManager root '{root.name}' to avoid duplicate singletons");
                UnityEngine.Object.Destroy(root);
            }
        }
    }

    [Tooltip("If true, runs LoadIntoGame in Start(). Disable for tests or for hot-reload sessions where you want a fresh run.")]
    [SerializeField] private bool _loadOnStart = true;

    private SaveData _pendingApply;

    // Use Awake instead of Start: sceneLoaded fires after every scene's
    // Awake/Start has run, so by the time this GameObject is created
    // and its Awake fires, GameScene systems (DataManager, Inventory,
    // FogController, DialogManager) are guaranteed to be available -
    // we don't have to wait an additional frame to avoid races with
    // the default-init systems.
    private void Awake()
    {
        // Always log Awake so debugging "save isn't loading" is straightforward -
        // if Awake's first line doesn't show up in Console, AutoCreate
        // and OnSceneLoaded never reached this instance, which means
        // the sceneLoaded pipeline is broken.
        Debug.Log($"[SaveBootstrap] Awake on '{SceneManager.GetActiveScene().name}' loadOnStart={_loadOnStart}");

        // (diag/input-after-continue) Reachability probe. After Continue we
        // suspect a Control/Inventory mismatch where the listener gets
        // wired to a stale Control that has nothing listening for input.
        // This diagnostic prints both sides so the next run's logs show
        // whether Awake, Start, and onTakeItem fire against the same
        // instance ID as Control.OnEnable reported.
        try
        {
            var control = FindAnyObjectByType<Control>();
            var inventory = FindAnyObjectByType<Inventory>();
            Debug.Log($"[SaveBootstrap] Probe -> Control={control?.GetInstanceID().ToString() ?? "null"} " +
                      $"Inventory={inventory?.GetInstanceID().ToString() ?? "null"} " +
                      $"subsOnOpenInventory={control?.OnOpenInventory?.GetInvocationList().Length ?? 0} " +
                      $"subsOnEsc={control?.OnEsc?.GetInvocationList().Length ?? 0}");
        }
        catch (System.Exception probeEx)
        {
            Debug.LogWarning($"[SaveBootstrap] probe failed: {probeEx.Message}");
        }

        try
        {
            if (!_loadOnStart) return;

            // Use SceneManager.GetActiveScene() instead of gameObject.scene:
            // this GameObject is parented to DontDestroyOnLoad (special
            // internal scene with no friendly name), so gameObject.scene.name
            // does not reflect the actual gameplay scene. The active scene
            // is what determines whether DataManager / Inventory /
            // FogController exist (they're Zenject scene-context
            // singletons).
            var activeScene = SceneManager.GetActiveScene().name;
            if (!activeScene.Contains("Game"))
            {
                Debug.Log($"[SaveBootstrap] Scene '{activeScene}' has no GameScene systems - skipping load.");
                return;
            }

            bool hasSave = SaveSystem.HasSave();
            Debug.Log($"[SaveBootstrap] GameScene detected, hasSave={hasSave}");
            if (!hasSave) return;

            SaveData data = SaveSystem.Load();
            if (data == null) return;

            // Apply money/hero/fog/map/dialogs/inventory. Wrap in
            // try/catch so a single broken component (e.g. an
            // ItemsManager.GetByIndex miss for a removed asset) doesn't
            // leave the entire scene half-initialised with broken input.
            try
            {
                GamePersistence.LoadIntoGame(data);
                Debug.Log($"[SaveBootstrap] LoadIntoGame OK money={data.money} inv={(data.inventory?.Count ?? 0)} fog={data.fogDensity}");
            }
            catch (System.Exception e)
            {
                // Dump the full stack so we can see which '.X.Y' on the load
                // path is throwing NullReferenceException. The previous
                // message-only log was useless - we need line info.
                Debug.LogWarning($"[SaveBootstrap] LoadIntoGame FAILED: {e.GetType().Name}: {e.Message}\nSTACK:\n{e.StackTrace}");
                // DO NOT delete the save automatically - if the load crashes
                // on every Continue, the player can't recover without a
                // manual reset, and silent wipe is a worse UX. Just leave
                // the save alone so the next Continue re-reads the same blob.
                return;
            }

            // Position needs the player to exist and Awake/Start to have run
            // for the CharacterController / camera. Defer one frame.
            _pendingApply = data;
        }
        catch (System.Exception outer)
        {
            // Outer guard catches anything that escapes before the inner
            // try (scene name lookup, etc.) so a single bug here can't
            // tear down the entire scene.
            Debug.LogError($"[SaveBootstrap] Outer Awake crash: {outer}");
        }
    }

    private void LateUpdate()
    {
        if (_pendingApply == null) return;
        var player = PlayerMovement.Instance;
        if (player != null && _pendingApply.position != null)
        {
            player.transform.position = new Vector3(
                _pendingApply.position.x,
                _pendingApply.position.y,
                _pendingApply.position.z);
        }
        _pendingApply = null;
    }
}
