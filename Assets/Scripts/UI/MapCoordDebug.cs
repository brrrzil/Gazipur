using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug overlay showing minimal minimap state. Toggle with F4
/// (F3 is taken by the FPS counter).
/// Mirrors FpsCounter's pattern: auto-bootstraps on play, renders
/// via IMGUI in the top-right corner so it doesn't depend on any
/// scene Canvas.
/// </summary>
public class MapCoordDebug : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (FindObjectOfType<MapCoordDebug>() != null) return;
        var go = new GameObject("[MapCoordDebug]");
        DontDestroyOnLoad(go);
        go.AddComponent<MapCoordDebug>();
    }

    [SerializeField] private Key _toggleKey = Key.F4;
    [SerializeField] private bool _visibleAtStart = false;
    [SerializeField] private float _updateInterval = 0.1f;

    private bool _visible;
    private float _accum;
    private Vector3 _playerPos;
    private float _playerYaw;
    private float _pixelsPerMeter;
    private float _playerArrowBaseAngle;
    private float _mapPixelRadius;
    private GUIStyle _style;
    private GUIStyle _headerStyle;

    void Awake()
    {
        _visible = _visibleAtStart;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[_toggleKey].wasPressedThisFrame)
            _visible = !_visible;

        _accum += Time.unscaledDeltaTime;
        if (_accum < _updateInterval) return;
        _accum = 0f;

        var pm = FindFirstObjectByType<PlayerMovement>();
        var map = FindFirstObjectByType<MapUI>();
        if (pm == null || map == null) return;

        _playerPos = pm.transform.position;
        _playerYaw = pm.transform.eulerAngles.y;

        var t = typeof(MapUI);
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        _pixelsPerMeter = (float)t.GetField("_pixelsPerMeter", bf).GetValue(map);
        _playerArrowBaseAngle = (float)t.GetField("_playerArrowBaseAngle", bf).GetValue(map);
        _mapPixelRadius = (float)t.GetField("_mapPixelRadius", bf).GetValue(map);
    }

    void OnGUI()
    {
        if (!_visible) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Normal
            };
            _headerStyle = new GUIStyle(_style)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
        }

        const float w = 280f;
        const float h = 110f;
        var bg = new Rect(Screen.width - w - 8, 8, w, h);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(bg, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float x = bg.x + 8;
        float y = bg.y + 6;
        float line = 18f;

        GUI.Label(new Rect(x, y, w - 16, line), "Map debug (F4 to hide)", _headerStyle); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Player world : X {_playerPos.x,7:0.00}   Z {_playerPos.z,7:0.00}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Player yaw   : {_playerYaw,7:0.0}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Arrow rot    : {-_playerYaw + _playerArrowBaseAngle,7:0.0}  ({-_playerYaw + _playerArrowBaseAngle + 360f,7:0.0})", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Pixels/m     : {_pixelsPerMeter:0.0}    Radius: {_mapPixelRadius:0}", _style);
    }
}
