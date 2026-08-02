using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Round 81: developer / debug cheat. Pressing the
/// configured key (default: P) adds a fixed amount
/// of money to the player's wallet through
/// DataManager.ChangeMoney.
///
/// The component is auto-bootstrapped at runtime
/// (no scene wiring needed) by the same
/// [RuntimeInitializeOnLoadMethod +
/// AfterSceneLoad] pattern FpsCounter.cs uses
/// (see FpsCounter.cs for the original pattern).
/// The script is intended for development
/// builds; if the user wants to disable the
/// cheat in release builds, wrap the
/// 'AutoBootstrap' body in '#if UNITY_EDITOR ||
/// DEVELOPMENT_BUILD' or strip the file from
/// release builds entirely.
///
/// Key handling uses the new Input System
/// (Key enum + Keyboard.current) to match the
/// project's policy of not using the legacy
/// Input class. The toggle key is a
/// [SerializeField] so the binding can be
/// remapped without editing the script.
/// </summary>
public class DebugCheat : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        // Skip if the scene already has a DebugCheat
        // attached to some GameObject; otherwise
        // create a fresh one. The same pattern is
        // used by FpsCounter.cs so the convention is
        // consistent across the project.
        if (FindFirstObjectByType<DebugCheat>() != null) return;
        var go = new GameObject("[DebugCheat]");
        // Round 75/76 lesson: a MonoBehaviour on its
        // own is useless (Awake never fires), so
        // DontDestroyOnLoad + auto-create is the
        // safe pattern for cross-scene debug helpers.
        DontDestroyOnLoad(go);
        go.AddComponent<DebugCheat>();
    }

    [SerializeField] private Key _cheatKey = Key.P;
    [SerializeField] private int _moneyAmount = 1000;

    private DataManager _data;

    private void Update()
    {
        // New Input System (round 64 project policy:
        // legacy Input.GetKey is not used anywhere in
        // the codebase). Keyboard.current is null on
        // platforms without a keyboard attached
        // (mobile, consoles) so we null-guard before
        // indexing.
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!kb[_cheatKey].wasPressedThisFrame) return;

        if (_data == null)
        {
            // Round 79 pattern: FindFirstObjectByType
            // is the Unity 6 replacement for the
            // deprecated FindObjectOfType. The lookup
            // is cached in '_data' so the scene-graph
            // search is paid at most once per cheat
            // press sequence.
            _data = FindFirstObjectByType<DataManager>();
        }

        if (_data == null)
        {
            // No DataManager in the scene: silently
            // bail out (debug helpers should never
            // throw a runtime error; the cheat simply
            // does nothing in the unlikely case the
            // scene is not the gameplay scene).
            return;
        }

        // DataManager.ChangeMoney already fires
        // 'onChangeMoney' (the UI money counter
        // subscribes to it and updates the
        // _moneyCount Text automatically) and
        // handles negative values for spend paths.
        _data.ChangeMoney(_moneyAmount);
    }
}
