using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-bootstrapped save/load entry point. Mirrors the pattern
/// FogController/FpsCounter use: a [RuntimeInitializeOnLoadMethod]
/// creates a hidden GameObject after scene load, the GameObject's
/// Start calls <see cref="GamePersistence.LoadIntoGame"/> with the
/// saved blob (if one exists).
///
/// No scene wiring needed. If the player drops a real SaveBootstrap
/// MonoBehaviour into a scene, that one takes over and the
/// auto-bootstrapped instance bails.
///
/// Position is applied in LateUpdate so the player teleport
/// doesn't fight cell initialisation and the CharacterController /
/// Cinemachine have had a frame to wake up.
///
/// New Game flow: SaveSystem.DeleteSave() wipes the slot. AfterSceneLoad
/// then calls LoadIntoGame which finds no save and is a no-op, so the
/// scene boots into authored defaults (DataManager.Start sets
/// _startMoney, FogController.Awake reads RenderSettings.fogDensity, etc.)
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

        // Hook sceneLoaded so a fresh SaveBootstrap is created when
        // SceneManager.LoadScene moves us from MainMenu to GameScene
        // (and back, and on Continue reload). The static AfterSceneLoad
        // callback above only fires once per app session, which is not
        // enough for a Continue-driven scene reload.
        if (!_subscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _subscribed = true;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only act on the GameScene. MainMenu doesn't need a
        // SaveBootstrap (no PlayerState to restore), and skipping
        // it avoids the GameScene's Inventory/items race when the
        // user is just sitting on the menu.
        if (!scene.name.Contains("Game")) return;
        if (FindFirstObjectByType<SaveBootstrap>() != null) return;
        var go = new GameObject("[SaveBootstrap]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SaveBootstrap>();
    }

    [Tooltip("If true, runs LoadIntoGame in Start(). Disable for tests or for hot-reload sessions where you want a fresh run.")]
    [SerializeField] private bool _loadOnStart = true;

    private SaveData _pendingApply;

    private void Start()
    {
        if (!_loadOnStart) return;

        bool hasSave = SaveSystem.HasSave();
        Debug.Log($"[SaveBootstrap] Start scene='{gameObject.scene.name}' hasSave={hasSave}");
        if (!hasSave) return;

        SaveData data = SaveSystem.Load();
        if (data == null) return;

        // Apply money/hero/fog/map/dialogs/inventory to whatever systems
        // are already alive in the scene by the time we reach Start.
        // Wrap in try/catch so a single broken component (e.g. an
        // ItemsManager.GetByIndex miss for a removed asset) doesn't
        // leave the entire scene half-initialised with broken input.
        try
        {
            GamePersistence.LoadIntoGame(data);
            Debug.Log($"[SaveBootstrap] LoadIntoGame OK money={data.money} inv={(data.inventory?.Count ?? 0)} fog={data.fogDensity}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveBootstrap] LoadIntoGame failed, falling back to scene defaults: {e.Message}");
            // Wipe the bad save so the next launch starts cleanly
            // instead of looping on the same broken blob.
            SaveSystem.DeleteSave();
            return;
        }

        // Position needs the player to exist and Awake/Start to have run
        // for the CharacterController / camera. Defer one frame.
        _pendingApply = data;
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
