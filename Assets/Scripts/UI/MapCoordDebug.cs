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
    private Vector3 _mapCenter;
    private float _pixelsPerMeter;
    private float _mapPixelRadius;
    private float _spriteShiftX;
    private float _spriteShiftY;
    private Vector2 _mapBackgroundActualPos;
    private Vector2 _playerArrowActualRot;
    private bool _backgroundFound;
    private bool _arrowFound;
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
        _mapCenter = (Vector3)t.GetField("_mapCenter", bf).GetValue(map);
        _pixelsPerMeter = (float)t.GetField("_pixelsPerMeter", bf).GetValue(map);
        _mapPixelRadius = (float)t.GetField("_mapPixelRadius", bf).GetValue(map);

        // Sprite pan at runtime: anchoredPosition = _spriteBasePos - delta * pixelsPerMeter.
        float dx = (_playerPos.x - _mapCenter.x) * _pixelsPerMeter;
        float dz = (_playerPos.z - _mapCenter.z) * _pixelsPerMeter;
        _spriteShiftX = -dx;
        _spriteShiftY = -dz;

        // Read the live RectTransform.anchoredPosition of the map
        // background to verify that the script really is moving it.
        _backgroundFound = false;
        var bgField = t.GetField("_mapBackground", bf);
        if (bgField != null)
        {
            var bg = bgField.GetValue(map) as RectTransform;
            if (bg != null)
            {
                _mapBackgroundActualPos = bg.anchoredPosition;
                _backgroundFound = true;
            }
        }

        _arrowFound = false;
        var arrowField = t.GetField("_playerArrow", bf);
        if (arrowField != null)
        {
            var arrow = arrowField.GetValue(map) as RectTransform;
            if (arrow != null)
            {
                _playerArrowActualRot = arrow.localRotation.eulerAngles;
                _arrowFound = true;
            }
        }
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

        const float w = 320f;
        const float h = 168f;
        var bg = new Rect(Screen.width - w - 8, 8, w, h);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(bg, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float x = bg.x + 8;
        float y = bg.y + 6;
        float line = 18f;

        GUI.Label(new Rect(x, y, w - 16, line), "Map debug (F4 to hide)", _headerStyle); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Player world   : X {_playerPos.x,7:0.00}   Z {_playerPos.z,7:0.00}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Map center     : X {_mapCenter.x,7:0.00}   Z {_mapCenter.z,7:0.00}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Delta          : X {(_playerPos.x - _mapCenter.x),7:+0.00;-0.00}   Z {(_playerPos.z - _mapCenter.z),7:+0.00;-0.00}", _style); y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Pan shift      : X {_spriteShiftX,7:+0.0;-0.0} px  Y {_spriteShiftY,7:+0.0;-0.0} px", _style); y += line;
        if (_backgroundFound)
            GUI.Label(new Rect(x, y, w - 16, line),
                $"BG actual      : X {_mapBackgroundActualPos.x,7:+0.0;-0.0} px  Y {_mapBackgroundActualPos.y,7:+0.0;-0.0} px  <-- LIVE", _style);
        else
            GUI.Label(new Rect(x, y, w - 16, line),
                "BG actual      : <missing _mapBackground ref>", _style);
        y += line;
        if (_arrowFound)
            GUI.Label(new Rect(x, y, w - 16, line),
                $"Arrow rot      : Z {_playerArrowActualRot.z,7:0.0}  <-- LIVE", _style);
        else
            GUI.Label(new Rect(x, y, w - 16, line),
                "Arrow rot      : <missing _playerArrow ref>", _style);
        y += line;
        GUI.Label(new Rect(x, y, w - 16, line),
            $"Pixels/m       : {_pixelsPerMeter:0.0}    Mask radius: {_mapPixelRadius:0}", _style);
    }
}
