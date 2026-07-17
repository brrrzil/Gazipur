using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Gazipur.Player
{
    /// <summary>
    /// (round 57) Right-mouse-button aim/zoom controller.
    /// Ported from another project where aim was triggered
    /// by Input.GetMouseButton(1). That legacy API throws
    /// InvalidOperationException in this project (new Input
    /// System only, Input.Get* is disabled in Player
    /// Settings), so we bind <Mouse>/rightButton via a
    /// runtime InputAction.
    ///
    /// While the right mouse button is held:
    ///   - FirstPersonCamera lens FieldOfView lerps
    ///     from _defaultFoV to _zoomFoV
    ///   - PlayerMovement.SetAimSlowdown(_aimSlowdown) is
    ///     called every frame so the player moves at a
    ///     reduced speed while aiming
    /// On release:
    ///   - FOV lerps back to _defaultFoV
    ///   - SetAimSlowdown(1f) restores normal speed
    /// </summary>
    public class AimController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("CinemachineCamera in the scene. FOV is driven by m_Lens.FieldOfView.")]
        [SerializeField] private CinemachineCamera _virtualCamera;

        [Header("FoV")]
        [Tooltip("FirstPersonCamera lens FieldOfView while right mouse button is held.")]
        [SerializeField] private float _zoomFoV = 35f;
        [Tooltip("Lerp factor per frame toward the target FoV. 0.1 matches the source project; lower = smoother, higher = snappier.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _zoomLerpSpeed = 0.1f;

        [Header("Movement")]
        [Tooltip("Multiplier passed to PlayerMovement.SetAimSlowdown while aiming. 0.25 = quarter speed, 0 = no movement. 1 = no slowdown (would be a no-op).")]
        [Range(0f, 1f)]
        [SerializeField] private float _aimSlowdown = 0.25f;

        [Header("Fog (round 58/59)")]
        [Tooltip("If true, also drive RenderSettings.fogDensity while aiming and back to the captured scene value on release. Fog is a per-fragment uniform in URP, so this is essentially free at runtime.")]
        [SerializeField] private bool _affectFog = true;
        [Tooltip("Multiplier applied to the captured default fog density while right mouse is held. 0.5 = half density (fog thins out, recommended default), 0 = no fog, 1 = unchanged, >1 = thicker. 0.5 matches the source project intent: aim-zoom thins the fog without erasing it.")]
        [Range(0f, 2f)]
        [SerializeField] private float _zoomFogMultiplier = 0.5f;

        private PlayerMovement _movement;
        private InputAction _aimAction;
        private bool _isAiming;
        // (round 58) Default FoV and default fog density are both
        // captured from the scene at first OnEnable (see
        // CaptureDefaults) so the Inspector does not need to
        // duplicate values that already live in CinemachineCamera
        // and RenderSettings.
        private float _defaultFoV;
        private float _defaultFogDensity;
        // Sentinel bools to avoid overwriting the captured values
        // every frame. Float defaults are 60 / 0.01 which are valid,
        // so we need a separate 'have we captured yet' flag.
        private bool _defaultFoVCaptured;
        private bool _defaultFogCaptured;

        [Inject]
        public void Construct(PlayerMovement movement)
        {
            // PlayerMovement is the same instance the rest of the game
            // uses, so SetAimSlowdown propagates without a singleton
            // lookup. If DI is broken (e.g. ProjectInstaller drift),
            // we will fall back to GetComponentInParent in Start.
            _movement = movement;
        }

        private void Awake()
        {
            // Runtime InputAction instead of editing
            // InputSystem_Actions.inputactions. <Mouse>/rightButton is
            // available because the new Input System has the Mouse
            // layout registered by default.
            _aimAction = new InputAction(
                name: "Aim",
                type: InputActionType.Button,
                binding: "<Mouse>/rightButton");
            _aimAction.performed += OnAimPerformed;
            _aimAction.canceled += OnAimCanceled;
        }

        private void OnEnable()
        {
            _aimAction.Enable();
            // Defensive: if Zenject did not inject (e.g. player object
            // spawned at runtime without ProjectInstaller), grab a
            // sibling PlayerMovement so the feature still works.
            if (_movement == null)
            {
                _movement = GetComponentInParent<PlayerMovement>();
                if (_movement == null)
                {
                    _movement = FindObjectOfType<PlayerMovement>();
                }
            }
            // Capture defaults the first time the component enables.
            // We do this in OnEnable (not Awake) so an Inspector
            // reference to _virtualCamera assigned during scene
            // loading is guaranteed to be present.
            CaptureDefaults();
        }

        private void OnDisable()
        {
            _aimAction.Disable();
            // Restore normal speed on disable so the player isn't
            // stuck at quarter speed if the component is hot-swapped
            // mid-aim.
            if (_movement != null)
            {
                _movement.SetAimSlowdown(1f);
            }
            // Restore fog density so the next enable (or another
            // system that reads RenderSettings.fogDensity) sees the
            // scene's original value, not a leftover 0 from zoom.
            if (_affectFog && _defaultFogCaptured && RenderSettings.fog)
            {
                RenderSettings.fogDensity = _defaultFogDensity;
            }
        }

        private void OnDestroy()
        {
            if (_aimAction != null)
            {
                _aimAction.performed -= OnAimPerformed;
                _aimAction.canceled -= OnAimCanceled;
                _aimAction.Dispose();
                _aimAction = null;
            }
        }

        private void OnAimPerformed(InputAction.CallbackContext ctx)
        {
            _isAiming = true;
        }

        private void OnAimCanceled(InputAction.CallbackContext ctx)
        {
            _isAiming = false;
        }

        private void CaptureDefaults()
        {
            // FoV: read from the camera the first time it is non-null.
            // Inspector reference is set during scene load, before
            // OnEnable, so this is reliable for the normal case.
            if (!_defaultFoVCaptured && _virtualCamera != null)
            {
                _defaultFoV = _virtualCamera.Lens.FieldOfView;
                _defaultFoVCaptured = true;
            }
            // Fog: capture once, regardless of fog state. If the
            // scene has fog disabled, RenderSettings.fogDensity is
            // still a valid number we can write back; the URP
            // fragment shader just ignores it because the global
            // 'fog' keyword is off. The guard in Update() and
            // OnDisable() checks RenderSettings.fog before pushing
            // the value, so this is safe.
            if (!_defaultFogCaptured && _affectFog)
            {
                _defaultFogDensity = RenderSettings.fogDensity;
                _defaultFogCaptured = true;
            }
        }

        private void Update()
        {
            // Drive the camera FoV. Cinemachine 3.x exposes Lens
            // directly: camera.Lens.FieldOfView (the YAML stores it
            // as m_Lens.FieldOfView in the asset, but at runtime it
            // is the LensSettings struct).
            if (_virtualCamera != null && _defaultFoVCaptured)
            {
                float targetFoV = _isAiming ? _zoomFoV : _defaultFoV;
                var lens = _virtualCamera.Lens;
                lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFoV, _zoomLerpSpeed);
                _virtualCamera.Lens = lens;
            }

            // Drive fog density on the same lerp. We guard on
            // RenderSettings.fog so we do not turn fog on for a
            // scene that intentionally has it off — if the user
            // wants aim-zoom fog in such a scene, they can tick
            // 'Fog' in Lighting > Scene tab and re-enable _affectFog.
            //
            // (round 59) target density is now a multiplier of the
            // captured default, not an absolute 0. 0.5 means
            // 'fog thins out by half while aiming' — that matches
            // the visual intent (the scene still has atmospheric
            // depth, you just see further through the scope) without
            // the harsh 'fog vanishes completely' feel of 0.
            if (_affectFog && _defaultFogCaptured && RenderSettings.fog)
            {
                float targetDensity = _isAiming
                    ? _defaultFogDensity * _zoomFogMultiplier
                    : _defaultFogDensity;
                RenderSettings.fogDensity = Mathf.Lerp(
                    RenderSettings.fogDensity, targetDensity, _zoomLerpSpeed);
            }

            // Drive the movement slowdown. We do it every frame
            // (not just on press/release) so a hot-set _aimSlowdown
            // value in the Inspector takes effect on the next frame
            // without having to release the button.
            if (_movement != null)
            {
                _movement.SetAimSlowdown(_isAiming ? _aimSlowdown : 1f);
            }
        }
    }
}
