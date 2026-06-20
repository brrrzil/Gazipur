using UnityEngine;

/// <summary>
/// Настройки производительности, применяемые на старте.
/// Вешается на GameManager (или любой объект, который есть в каждой сцене).
/// </summary>
[DefaultExecutionOrder(-10000)]
public class PerformanceConfig : MonoBehaviour
{
    [Header("Frame Rate")]
    [SerializeField] private int _targetFrameRate = 60;
    [SerializeField] private bool _disableVSync = true;

    void Awake()
    {
        Application.targetFrameRate = _targetFrameRate;
        if (_disableVSync)
        {
            // VSync перебивает targetFrameRate в Editor на некоторых платформах.
            QualitySettings.vSyncCount = 0;
        }
    }
}
