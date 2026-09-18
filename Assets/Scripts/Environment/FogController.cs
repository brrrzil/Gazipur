using UnityEngine;

/// <summary>
/// Single owner of <see cref="RenderSettings.fogDensity"/>. Two
/// systems change it: GarbageObject pickup (gradual thinning) and
/// AimController (temporary zoom thinning). When both write the
/// RenderSettings directly they clobber each other — aim releases
/// restoring the captured scene default cancels the pickup thining.
///
/// FogController reads the scene's current density at boot, owns a
/// "cleared" value that pickups push down, and lerps the live density
/// toward whatever target was last requested (cleared base or cleared
/// base × aim multiplier). Both subsystems then only have to call
/// intent methods; they never touch RenderSettings themselves.
///
/// Density always resets to the scene's authored value at every
/// launch — pickup work does NOT carry between sessions.
/// </summary>
public class FogController : MonoBehaviour
{
    public static FogController Instance { get; private set; }

    [SerializeField] private float _lerpSpeed = 0.05f;

    // The baseline the scene is thining toward (decreased by pickup).
    // Re-read from RenderSettings every Awake so a fresh launch starts
    // from the scene's authored value, not the previous run's leftovers.
    private float _clearedDensity;
    // Whatever density we're showing right now, lerping toward _targetDensity.
    private float _liveDensity;
    // Set by external systems; FogController lerps _liveDensity toward it.
    private float _targetDensity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Reset to the scene's authored density every launch. If you
        // want pickups to persist, add a separate "save game" hook
        // instead of touching this.
        _clearedDensity = RenderSettings.fogDensity;
        _liveDensity = _clearedDensity;
        _targetDensity = _clearedDensity;
        RenderSettings.fogDensity = _liveDensity;
    }

    private void Update()
    {
        if (Mathf.Approximately(_liveDensity, _targetDensity)) return;
        _liveDensity = Mathf.Lerp(_liveDensity, _targetDensity, _lerpSpeed);
        // Snap to the target once we get close enough so the live value
        // visibly settles. Without this, Mathf.Lerp never reaches the
        // target exactly and the Inspector shows a value 1e-6 off the
        // expected one until the user nudges it.
        if (Mathf.Abs(_liveDensity - _targetDensity) < 0.0005f) _liveDensity = _targetDensity;
        RenderSettings.fogDensity = _liveDensity;
    }

    /// <summary>The cleared (post-pickup) fog density. Use this to read
    /// what the world would be if aim weren't pressed.</summary>
    public float ClearedDensity => _clearedDensity;

    /// <summary>Push the cleared density down by <paramref name="amount"/>,
    /// clamped at 0. Aim zoom follows along automatically because
    /// multiplies against the cleared value.</summary>
    public void DecreaseFog(float amount)
    {
        _clearedDensity = Mathf.Max(0f, _clearedDensity - amount);
        _targetDensity = _clearedDensity;
    }

    /// <summary>Set the live density toward cleared × <paramref name="multiplier"/>.
    /// Pass 1 to come back to cleared; pass 0 to wipe fog while zoomed in.</summary>
    public void AimMultiply(float multiplier)
    {
        // Clamp the multiplier to >= 0 so an unexpected -1 from a
        // buggy caller can't push the target below zero.
        float newTarget = Mathf.Max(0f, _clearedDensity * Mathf.Max(0f, multiplier));
        // Snap immediately when the multiplier is 1 — that's the
        // "release aim" path, the player expects the fog back at full
        // density the moment they let go of right mouse.
        _targetDensity = newTarget;
        if (Mathf.Approximately(multiplier, 1f))
        {
            _liveDensity = newTarget;
            RenderSettings.fogDensity = _liveDensity;
        }
    }

    /// <summary>Snap the live density back to cleared without waiting on lerp.</summary>
    public void RestoreDefault()
    {
        _liveDensity = _clearedDensity;
        _targetDensity = _clearedDensity;
        RenderSettings.fogDensity = _liveDensity;
    }
}

/// <summary>
/// Auto-bootstrap a FogController if the scene has none. Mirrors the
/// patterns used by FpsCounter and MapCoordDebug.
/// </summary>
public static class FogControllerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (FogController.Instance != null) return;
        var go = new GameObject("[FogController]");
        // DontDestroyOnLoad is an instance method on Object, so call
        // it through UnityEngine.Object explicitly from this static
        // helper.
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<FogController>();
    }
}
