using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// (r5 / ddol-gamescene) Promotes the GameScene's root GameObjects to
/// DontDestroyOnLoad on first load. After this runs, GameScene behaves
/// as if it was always alive - no scene reload happens when DiePanel /
/// WinPanel click "Try Again", SceneLoader.LoadScene(1) short-circuits
/// into GameSession.RestartFromCurrentSave, and every SerializeField
/// UI ref on Canvas-bound MonoBehaviours stays valid forever.
///
/// Why a separate auto-bootstrap and not just inline this in
/// GameSession: the keeper has to run AFTER scene load (so the scene's
/// root objects exist) but BEFORE anything else on GameScene tries to
/// grab SerializeField refs or register OnSceneLoaded callbacks. Hooking
/// SceneManager.sceneLoaded from a static [RuntimeInitializeOnLoadMethod]
/// is the cleanest way to get that timing right - it fires after every
/// scene's Awake/Start has run, so by the time we move roots the
/// Zenject SceneContext has installed every [Inject].
///
/// Why we wait one frame before moving: Unity does not allow
/// DontDestroyOnLoad on a root GameObject until that root has finished
/// activating. Calling it on the same frame as the scene load sometimes
/// works, sometimes logs 'DontDestroyOnLoad only works for root
/// GameObjects' if the object was just instantiated in the same frame.
/// Yielding one frame is the cheapest workaround.
///
/// Edge case: if the player reloads GameScene from scratch (e.g. via
/// a fallback path before this hook runs), Unity loads a fresh
/// GameScene alongside our DDOL one - we then have two sets of root
/// objects. The de-dup lives in SceneLoader's 'real LoadScene' branch,
/// not here, because we don't want to destroy anything until we know
/// it's actually a duplicate.
/// </summary>
public static class GameSceneKeeper
{
    private const string HostName = "[GameScene (DDOL)]";
    private static GameObject _host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoHook()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Also handle the case where the very first scene IS the
        // GameScene (no MainMenu) - sceneLoaded won't fire retroactively,
        // so check the active scene right now.
        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name.Contains("Game"))
        {
            OnSceneLoaded(active, LoadSceneMode.Single);
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.Contains("Game")) return;
        if (_host != null)
        {
            // We already have a DDOL host from a previous load. The
            // scene's roots are duplicates (or were created by a real
            // LoadScene that bypassed SceneLoader's short-circuit).
            // Destroy the new roots so we don't end up with two
            // stacked GameScenes. The DDOL set is the canonical one.
            Debug.LogWarning($"[GameSceneKeeper] GameScene re-loaded with host already present; destroying fresh roots.");
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && root != _host && root.scene == scene)
                    Object.Destroy(root);
            }
            return;
        }

        // (r5 / dotween-crash) Sounds.Init() runs an [Inject] method
        // that calls DontDestroyOnLoad(transform.root.gameObject) on
        // the GameManager hierarchy - the same hierarchy we're about
        // to reparent. By the time OnSceneLoaded fires, that root
        // is ALREADY in the DDOL scene. If we then take every root
        // and SetParent it under our [GameScene (DDOL)] host, the
        // GameManager root is being moved from DDOL into DDOL - which
        // Unity handles, but every DOTween animation that started on
        // a RectTransform under that root fires a "Tween startup
        // failed - RectTransform has been destroyed" because the
        // parent change tears down the cached RectTransform binding
        // that DOTween captured at Start.
        //
        // Skip the reparent step entirely when the scene's roots are
        // already in DDOL. Set _host anyway so IsGameSceneLoaded
        // flips and subsequent restart paths route through
        // GameSession instead of SceneManager.LoadScene.
        bool anyRootInDDOL = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.scene.buildIndex == -1) // DontDestroyOnLoad uses buildIndex=-1
            {
                anyRootInDDOL = true;
                break;
            }
        }
        if (anyRootInDDOL)
        {
            // Bootstrap a fresh host as a placeholder so IsGameScene-
            // Loaded has something to point at. The DDOL roots
            // themselves stay where Sounds.Init put them - that's
            // exactly what we want, no churn.
            _host = new GameObject(HostName);
            Object.DontDestroyOnLoad(_host);
            Debug.Log($"[GameSceneKeeper] GameScene roots already in DDOL (via Sounds.Init). Using existing DDOL set; no reparent.");
            return;
        }

        // Spin up a host on a fresh coroutine so we can yield one
        // frame before DontDestroyOnLoad (works around Unity's
        // 'cannot DDOL on the same frame as creation' warning).
        var bootstrapGo = new GameObject("[GameSceneKeeper]");
        Object.DontDestroyOnLoad(bootstrapGo);
        var runner = bootstrapGo.AddComponent<KeeperRunner>();
        runner.StartCoroutine(runner.Promote(scene));
    }

    /// <summary>Public accessor for SceneLoader / tests.</summary>
    public static bool IsGameSceneInDDOL => _host != null;

    private class KeeperRunner : MonoBehaviour
    {
        public IEnumerator Promote(Scene scene)
        {
            // Yield one frame to let scene activation complete.
            yield return null;
            if (_host != null) yield break;

            _host = new GameObject(HostName);
            Object.DontDestroyOnLoad(_host);

            // Reparent every root of the freshly loaded GameScene under
            // our DDOL host. SetParent(worldPositionStays:false) so we
            // don't teleport anything; scene authors placed objects
            // exactly where they wanted them.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                // Skip the bootstrap GameObject we just spawned (it's
                // parented to DDOL already and never had a scene root).
                if (root == gameObject) continue;
                if (root == _host) continue;
                root.transform.SetParent(_host.transform, worldPositionStays: false);
            }

            Debug.Log($"[GameSceneKeeper] Promoted GameScene '{scene.name}' to DDOL under '{HostName}'.");
        }
    }
}
