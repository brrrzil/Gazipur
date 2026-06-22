using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lightweight runtime FPS counter. Toggle with F3.
/// Renders via IMGUI in the top-left corner so it doesn't depend on any
/// scene Canvas. Auto-bootstraps on play.
/// </summary>
public class FpsCounter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (FindObjectOfType<FpsCounter>() != null) return;
        var go = new GameObject("[FpsCounter]");
        DontDestroyOnLoad(go);
        go.AddComponent<FpsCounter>();
    }

    [SerializeField] private Key _toggleKey = Key.F3;
    [SerializeField] private bool _visibleAtStart = false;
    [SerializeField] private float _updateInterval = 0.5f;

    private bool _visible;
    private float _accum;
    private int _frames;
    private float _displayFps;
    private GUIStyle _style;

    void Awake()
    {
        _visible = _visibleAtStart;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[_toggleKey].wasPressedThisFrame)
            _visible = !_visible;

        _frames++;
        _accum += Time.unscaledDeltaTime;
        if (_accum >= _updateInterval)
        {
            _displayFps = _frames / _accum;
            _frames = 0;
            _accum = 0f;
        }
    }

    void OnGUI()
    {
        if (!_visible) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };
        }
        var rect = new Rect(8, 8, 160, 28);
        GUI.color = new Color(0, 0, 0, 0.55f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(12, 10, 156, 24),
            $"FPS: {_displayFps:0}  |  ms: {(1000f / Mathf.Max(1f, _displayFps)):0.0}",
            _style);
    }
}
