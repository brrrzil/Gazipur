using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// (r5 / ddol-gamescene) Auto-bootstrapped save/load entry point.
/// Subscribes to SceneManager.sceneLoaded once and runs
/// <see cref="GamePersistence.LoadIntoGame"/> on the first GameScene
/// load. Subsequent in-place restarts (DiePanel Try Again) are handled
/// by GameSession.RestartFromCurrentSave - this class no longer needs
/// a LateUpdate deferred-position path or a sceneUnloaded hook because
/// GameScene itself lives in DDOL now and never reloads.
///
/// Load timing: AutoCreate runs AfterSceneLoad of the first scene
/// (MainMenu), so by the time the player hits Continue and
/// SceneManager.LoadScene moves us to GameScene the sceneLoaded
/// hook fires once with the GameScene loaded. We do NOT recreate this
/// component on every GameScene load because there is no every - the
/// scene is in DDOL after the first transition.
///
/// Position handling: moved to GameSession.RestartFromCurrentSave.
/// SaveBootstrap no longer defers a position because there is no
/// 'one frame after Awake' to defer to (Awake fires once for the
/// session, not per Restart).
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

        if (!_subscribed)
        {
            // (r5) Only sceneLoaded. sceneUnloaded is gone because
            // GameScene never unloads after the first transition
            // (it lives in DDOL). The previous DestroyStaleGame-
            // ManagerFromDDOL logic that ran on sceneUnloaded is no
            // longer needed for the same reason: there is no duplicate
            // GameManager being hoisted into DDOL on every reload.
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only restore on the GameScene. MainMenu has no PlayerState /
        // Inventory / FogController, so a Start() there would just
        // do nothing useful. Skipping it also keeps the player from
        // fighting inventory state when they're just sitting on the menu.
        if (!scene.name.Contains("Game")) return;

        // (r5) We do NOT recreate SaveBootstrap here. The component
        // already exists in DDOL from AutoCreate; its Awake fires
        // when the very first MainMenu loads. Re-creating it was the
        // previous pattern because SceneManager.LoadScene used to
        // unload our GameObject along with the rest of the scene.
        // Now that we're DDOL, Awake runs once for the app session,
        // and that's enough - GameSession.RestartFromCurrentSave
        // handles every later 'restart' click.
        //
        // What we DO need here is a sanity check: if the
        // auto-bootstrapped SaveBootstrap's Awake bailed because it
        // ran during MainMenu (no GameScene systems yet), we want to
        // run the load now. Use FindAnyObjectByType + a flag field.
        var sb = FindAnyObjectByType<SaveBootstrap>();
        if (sb != null) sb.TryLoadNow();
    }

    [Tooltip("If true, runs LoadIntoGame in Start(). Disable for tests or for hot-reload sessions where you want a fresh run.")]
    [SerializeField] private bool _loadOnStart = true;

    // (r5) Awake flag: 0 = waiting for GameScene load, 1 = already
    // ran the load. Set to 1 from Awake (when GameScene is already
    // active on first launch) and from TryLoadNow (when OnSceneLoaded
    // routes a late GameScene load into us).
    private int _loadState;

    private void Awake()
    {
        Debug.Log($"[SaveBootstrap] Awake on '{SceneManager.GetActiveScene().name}' loadOnStart={_loadOnStart}");

        // Awake may run on MainMenu (the typical first scene). In that
        // case the GameScene systems are not yet available, so we wait
        // for OnSceneLoaded to call TryLoadNow. If Awake runs directly
        // on GameScene (the user launched the player into GameScene
        // without going through MainMenu), we load immediately.
        if (SceneManager.GetActiveScene().name.Contains("Game"))
        {
            TryLoadNow();
        }
        else
        {
            Debug.Log("[SaveBootstrap] Awake on non-Game scene; deferring load to OnSceneLoaded.");
        }
    }

    public void TryLoadNow()
    {
        if (_loadState != 0) return; // already loaded once
        if (!_loadOnStart) return;

        try
        {
            var activeScene = SceneManager.GetActiveScene().name;
            if (!activeScene.Contains("Game"))
            {
                Debug.Log($"[SaveBootstrap] Scene '{activeScene}' has no GameScene systems - skipping load.");
                return;
            }

            bool hasSave = SaveSystem.HasSave();
            Debug.Log($"[SaveBootstrap] GameScene detected, hasSave={hasSave}");
            if (!hasSave)
            {
                _loadState = 1;
                return;
            }

            SaveData data = SaveSystem.Load();
            if (data == null) { _loadState = 1; return; }

            try
            {
                GamePersistence.LoadIntoGame(data);
                Debug.Log($"[SaveBootstrap] LoadIntoGame OK money={data.money} inv={(data.inventory?.Count ?? 0)} fog={data.fogDensity}");
                _loadState = 1;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveBootstrap] LoadIntoGame FAILED: {e.GetType().Name}: {e.Message}\nSTACK:\n{e.StackTrace}");
                // DO NOT delete the save automatically. Leave it for
                // the next Continue to re-read.
            }

            // (r5) Position is applied inline by GameSession.Restart-
            // FromCurrentSave. The previous _pendingApply LateUpdate
            // pattern was needed when SaveBootstrap re-ran Awake on
            // every scene reload; with GameScene in DDOL there's no
            // reload, so the deferred position has no consumer.
        }
        catch (System.Exception outer)
        {
            Debug.LogError($"[SaveBootstrap] Outer Awake crash: {outer}");
        }
    }
}
