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
        // Flip the flag for any scene whose name contains "Game" - both
        // the live GameScene and any future variants (GameScene_Forest,
        // etc.) should still be eligible for in-place restart. The
        // MainMenu's own sceneLoaded will simply not match this check.
        if (!scene.name.Contains("Game")) return;
        Instance.IsGameSceneLoaded = true;
        Debug.Log($"[GameSession] GameScene flagged as loaded ('{scene.name}').");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by SceneLoader.LoadScene(1) when GameScene is
    /// already loaded (the common Try-Again-on-DiePanel path). Skips a
    /// real SceneManager.LoadScene - Unity would tear down the entire
    /// GameScene hierarchy only to recreate it from the prefab, which
    /// is the source of every 'subscribers break on load' bug we have
    /// been chasing. Instead: apply the saved blob, close any open UI,
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
}
