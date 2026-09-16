using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

/// <summary>
/// Debug overlay showing the player's world position and the
/// corresponding sprite-pixel position on the map. Toggle with F4
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
    private Vector3 _mapCenter;
    private Vector2 _mapPixelSize;
    private float _pixelsPerMeter;
    private float _playerSpritePxX;
    private float _playerSpritePxY;
    private float _spriteOriginX;
    private float _spriteOriginZ;
    private float _shiftX;
    private float _shiftY;
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
        // Read the same fields the MapUI Inspector shows so the
        // overlay matches what the user configured on the component.
        // The fields are private; we read them through SerializedObject
        // reflection to avoid widening the API just for a debug
        // overlay. Reflection cost is paid once per refresh interval
        // (10 Hz), not per frame.
        var t = typeof(MapUI);
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        _mapCenter = (Vector3)t.GetField("_mapCenter", bf).GetValue(map);
        _mapPixelSize = (Vector2)t.GetField("_mapPixelSize", bf).GetValue(map);
        _pixelsPerMeter = (float)t.GetField("_pixelsPerMeter", bf).GetValue(map);

        float worldPerPixel = 1f / Mathf.Max(0.01f, _pixelsPerMeter);
        _spriteOriginX = _mapCenter.x - _mapPixelSize.x * 0.5f * worldPerPixel;
        _spriteOriginZ = _mapCenter.z - _mapPixelSize.y * 0.5f * worldPerPixel;

        float deltaX = _playerPos.x - _mapCenter.x;
        float deltaZ = _playerPos.z - _mapCenter.z;
        _playerSpritePxX = (_playerPos.x - _spriteOriginX) * _pixelsPerMeter;
        _playerSpritePxY = (_playerPos.z - _spriteOriginZ) * _pixelsPerMeter;
        _shiftX = -deltaX * _pixelsPerMeter;
        _shiftY = -deltaZ * _pixelsPerMeter;
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
        const float h = 132f;
        // Top-right corner of the screen.
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
            $"Map center   : X {_mapCenter.x,7:0.00}   Z {_mapCenter.z,7:0.00}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Sprite origin : X {_spriteOriginX,7:0.00}   Z {_spriteOriginZ,7:0.00}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Player sprite : X {_playerSpritePxX,7:0.0} px  Y {_playerSpritePxY,7:0.0} px", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Sprite shift  : X {_shiftX,7:0.0} px  Y {_shiftY,7:0.0} px", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Pixel size    : {_mapPixelSize.x:0} x {_mapPixelSize.y:0}   px/m {_pixelsPerMeter:0.0}", _style);
    }
}
