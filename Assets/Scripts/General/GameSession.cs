using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// (r5 / ddol-gamescene) Single entry point for "transition into GameScene"
/// and "reset/continue the live GameScene". Replaces the previous pattern
/// where DiePanel/WinPanel called SceneLoader.LoadScene(1, Single) to
/// force a full Unity scene reload.
///
/// Why a new orchestrator instead of inlining the reset into
/// SceneLoader: every call site that needs a "fresh GameScene" runs the
/// same five-step recipe (close any open panels, hide cursor, drop back
/// to GameMode.outdors, reapply SaveData, re-seed start-items if no
/// save). Centralising it here means the recipe lives in one place -
/// adding a new "restart"-style button is one GameSession call.
///
/// Activation: GameSceneKeeper creates a [GameScene (DDOL)] host on
/// the first GameScene load. GameSession exposes ActivateHost() and
/// DeactivateHost() so the rest of the project can flip that host's
/// activeSelf without poking into GameSceneKeeper's internals. Every
/// transition into / out of gameplay goes through these two methods,
/// and they handle the cross-cutting concerns (cursor, game mode, save)
/// in one place.
///
/// Lifecycle: GameSession itself is auto-bootstrapped before any scene
/// loads ([RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]), so by the
/// time MainMenuScript.OnNewGame or SceneLoader.LoadScene(1) runs, the
/// instance already exists and is reachable as GameSession.Instance.
///
/// It lives in DDOL so it survives MainMenu → GameScene transitions and
/// is the same instance the WinDiePanel continues / restarts buttons
/// see when they fire on the GameScene side.
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    /// <summary>True once GameSceneKeeper has promoted a GameScene load
    /// into DDOL. Used by SceneLoader to short-circuit
    /// LoadScene(1, Single) into a logical restart.</summary>
    public bool IsGameSceneLoaded { get; private set; }

    /// <summary>True when the GameScene host is currently active.
    /// Flipped to false on MainMenu entry, true on GameScene entry.
    /// The flag is what the rest of the project reads when it wants
    /// to know whether gameplay is currently live.</summary>
    public bool IsHostActive => _hostActive;

    /// <summary>The DDOL host created by GameSceneKeeper. May be null
    /// before the first GameScene load.</summary>
    public GameObject Host => _host;

    private GameObject _host;
    private bool _hostActive;
    // (r5) If OnSceneLoaded fires for a GameScene before
    // GameSceneKeeper has built the host, we remember the activation
    // request and replay it once the host appears. Without this the
    // first activation is lost (sceneLoaded fires before the keeper's
    // coroutine completes).
    private bool _pendingActivate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[GameSession]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameSession>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance == null) return;
        bool isGameScene = scene.name.Contains("Game");
        if (isGameScene)
        {
            // Flip the flag so subsequent SceneLoader.LoadScene(1)
            // calls route into RestartFromCurrentSave instead of a
            // real LoadScene.
            Instance.IsGameSceneLoaded = true;
            Instance.ActivateHost();
            Debug.Log($"[GameSession] GameScene entered ('{scene.name}').");
        }
        else
        {
            // MainMenu (or any future non-game scene). Drop the
            // GameScene flag - if the player clicks something that
            // goes to GameScene later, SceneLoader.LoadScene(1) will
            // see the flag is false and do a real LoadScene.
            Instance.IsGameSceneLoaded = false;
            Instance.DeactivateHost();
            Debug.Log($"[GameSession] Non-game scene entered ('{scene.name}'); GameScene host deactivated.");
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    /// <summary>(r5) GameSceneKeeper calls this once it has built the
    /// DDOL host. Stores the reference; if a previous sceneLoaded had
    /// already requested activation, replays it now.</summary>
    public void RegisterHost(GameObject host)
    {
        if (host == null) return;
        _host = host;
        if (_pendingActivate)
        {
            _pendingActivate = false;
            ActivateHost();
        }
    }

    /// <summary>Turn the GameScene host on, drop back to GameMode.outdors,
    /// lock the cursor. Safe to call when no host exists yet - sets the
    /// pending flag so the next RegisterHost call replays the request.
    /// Safe to call repeatedly.</summary>
    public void ActivateHost()
    {
        if (_host == null)
        {
            _pendingActivate = true;
            return;
        }
        if (_hostActive) return; // idempotent: re-activation is a no-op
        _hostActive = true;
        _host.SetActive(true);
        // Drop back to GameMode.outdors so the player can actually
        // move. The first GameScene load runs SaveBootstrap.LoadIntoGame
        // BEFORE we get here (SaveBootstrap subscribes to sceneLoaded
        // after us, but LoadIntoGame itself doesn't reset gameMode
        // because gameMode isn't persisted - it would still hold the
        // last value the player left it at, e.g. 'menu' if they paused
        // before quitting). Forcing outdors here gives a clean entry
        // state on every activation.
        var gmm = FindAnyObjectByType<GameModeManager>();
        if (gmm != null) gmm.OutDors();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        var sounds = FindAnyObjectByType<Sounds>();
        if (sounds != null) sounds.SwitchToGameBackground();
        Debug.Log("[GameSession] ActivateHost: host active, mode=outdors");
    }

    /// <summary>Turn the GameScene host off, save current state, show
    /// cursor. Safe to call repeatedly; safe to call when no host
    /// exists (just sets _hostActive=false and saves).</summary>
    public void DeactivateHost()
    {
        _hostActive = false;
        if (_host != null) _host.SetActive(false);
        // Snapshot whatever the player did before they hit MainMenu so
        // Continue picks up where they left off. SaveNow is cheap (a
        // single JsonUtility.ToJson + PlayerPrefs.SetString).
        try { GamePersistence.SaveNow(); }
        catch (System.Exception e) { Debug.LogWarning($"[GameSession] SaveNow on deactivate failed: {e.Message}"); }
        // Show cursor - if MainMenu buttons need to be clicked, the
        // cursor has to be visible.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        Debug.Log("[GameSession] DeactivateHost: host hidden, saved");
    }

    /// <summary>Called by SceneLoader.LoadScene(1) when GameScene is
    /// already loaded (the common Try-Again-on-DiePanel path). Skips a
    /// real SceneManager.LoadScene - Unity would tear down the entire
    /// GameScene hierarchy only to recreate it from the prefab, which
    /// is the source of every 'subscribers break on load' bug we have
    /// been chasing. Instead: apply the save blob, close any open UI,
    /// drop to outdors mode, re-position the player.</summary>
    public void RestartFromCurrentSave()
    {
        Debug.Log("[GameSession] RestartFromCurrentSave");

        // 1) Hide the death / win panel that triggered this. Resolve by
        //    component so this works whether the panel is on a
        //    GameScene root or inside the Canvas tree.
        var die = FindAnyObjectByType<WinDiePanel>();
        if (die != null) die.gameObject.SetActive(false);

        // 2) Apply the saved blob (if one exists). LoadIntoGame
        //    re-seeds inventory, money, fog, quests, dialog flags.
        //    Position is intentionally NOT applied by LoadIntoGame -
        //    it relies on SaveBootstrap.LateUpdate reading a
        //    _pendingApply field that only gets set when SaveBootstrap
        //    wakes up. With GameScene in DDOL SaveBootstrap only wakes
        //    once, so a Restart triggered after the first load would
        //    silently skip the player teleport. Apply position here
        //    instead, right after LoadIntoGame has populated
        //    data.position.
        //    Wrap in try/catch so a single broken component (e.g. an
        //    ItemsManager.GetByIndex miss for a removed asset) doesn't
        //    leave the scene half-initialised.
        SaveData dataForApply = null;
        if (SaveSystem.HasSave())
        {
            try
            {
                dataForApply = SaveSystem.Load();
                if (dataForApply != null) GamePersistence.LoadIntoGame(dataForApply);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameSession] LoadIntoGame failed: {e}");
            }
        }
        else
        {
            // No save blob. The previous behaviour (scene reload) would
            // silently re-run Start() on every MonoBehaviour, restoring
            // their authored defaults. We can't do that here because
            // GameScene is now in DDOL and Start() doesn't re-run. Tell
            // the user and continue - inventory / fog / quests are left
            // as they were on the previous play session, which is a
            // soft bug but better than NREs. Phase 6 will introduce
            // per-component Reset() methods that we call here.
            Debug.LogWarning("[GameSession] RestartFromCurrentSave: no save blob, components left in their current state (Phase 6 will add Reset methods).");
        }

        // 3) Drop back to GameMode.outdors. The previous mode (die /
        //    win) left _isUIMode=true on PlayerMovement, which freezes
        //    the player. OutDors() sets timeScale back to 1, flips
        //    PlayerMovement.SetMode, hides the cursor.
        var gmm = FindAnyObjectByType<GameModeManager>();
        if (gmm != null) gmm.OutDors();

        // 4) Hide cursor + lock it. OutDors -> ChangeMode(outdors) ->
        //    PlayerMovement.SetMode already does this, but we belt-and-
        //    brace in case a future panel re-shows it.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 5) Switch background music back to game (DiePanel/WinPanel
        //    listeners switched it to die/win on entry).
        var sounds = FindAnyObjectByType<Sounds>();
        if (sounds != null) sounds.SwitchToGameBackground();

        // 6) Apply player position. LoadIntoGame used to do this via
        //    SaveBootstrap.LateUpdate's _pendingApply field, but that
        //    only works for the FIRST scene load - on Restart we have
        //    no fresh Awake. Apply directly so the player spawns where
        //    the save says (typically the respawn anchor after a die).
        if (dataForApply != null && dataForApply.position != null)
        {
            var player = PlayerMovement.Instance;
            if (player != null)
            {
                player.transform.position = new Vector3(
                    dataForApply.position.x,
                    dataForApply.position.y,
                    dataForApply.position.z);
            }
        }

        Debug.Log("[GameSession] RestartFromCurrentSave done");
    }

    /// <summary>(r5 / new-game) Reset every persisted singleton back to
    /// its scene-authored default. Called from MainMenuScript.OnNewGame
    /// BEFORE the GameScene (re)loads. Without this, GameScene roots
    /// live in DDOL and SceneManager.LoadScene("GameScene") is a no-op
    /// for them - the previous run's Money / Inventory / Quest state
    /// would carry over because Awake/Start only fire once per session.
    ///
    /// Each system owns its own ResetToSceneDefaults() so adding a new
    /// persisted system is one line here plus one Reset() in the new
    /// system. The orchestrator's job is to call them all in the right
    /// order and surface failures.
    ///
    /// Order matters where systems depend on each other: DataManager
    /// first (clears money / hero / inventory cache), Inventory next
    /// (re-seeds start items, syncs DataManager), then per-scene
    /// systems (Fog, Quest, Dialog, Map, WaterFilter).
    public void ResetForNewGame()
    {
        Debug.Log("[GameSession] ResetForNewGame");

        // 0) Wipe the slot first so any SaveNow that fires during the
        //    reset (DataManager.ChangeMoney) writes a 'fresh' state
        //    rather than overwriting the previous run's slot.
        SaveSystem.DeleteSave();

        // 1) Reset state-bearing singletons. Each one is optional
        //    (FindAnyObjectByType can return null on a misconfigured
        //    project) so we null-check before calling.
        var data = DataManager.Instance;
        if (data != null) data.ResetToDefaults();

        var inv = Inventory.Instance;
        if (inv != null) inv.ResetToDefaults();

        var fog = FogController.Instance;
        if (fog != null) fog.ResetToSceneDefaults();

        var qm = QuestManager.Instance;
        if (qm != null) qm.ResetToSceneDefaults();

        var dialog = DialogManager.Instance;
        if (dialog != null) dialog.ResetToSceneDefaults();

        var map = MapUI.Instance;
        if (map != null) map.ResetToSceneDefaults();

        // Loot registry: every per-pickup flag (filter parts, collected
        // items, etc.) so they all respawn on the new run.
        LootPersistence.ClearAll();

        // 2) Drop back to outdors mode and reset cursor state.
        var gmm = FindAnyObjectByType<GameModeManager>();
        if (gmm != null) gmm.OutDors();

        // 3) Trigger one SaveNow so the freshly-cleared state is on
        //    disk before the user does anything. If we skip this,
        //    the next GamePersistence.SaveNow from any producer (money
        //    change, item pickup) would overwrite the cleared state
        //    with whatever the producer saw at that moment - usually
        //    the previous run's values lingering from the in-memory
        //    caches.
        try { GamePersistence.SaveNow(); }
        catch (System.Exception e) { Debug.LogWarning($"[GameSession] ResetForNewGame final save failed: {e.Message}"); }

        Debug.Log("[GameSession] ResetForNewGame done");
    }
}
