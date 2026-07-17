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
        [Tooltip("FirstPersonCamera lens FieldOfView when not aiming. Matches the m_Lens.FieldOfView in scene (60 in GameScene at HEAD).")]
        [SerializeField] private float _defaultFoV = 60f;
        [Tooltip("FirstPersonCamera lens FieldOfView while right mouse button is held.")]
        [SerializeField] private float _zoomFoV = 35f;
        [Tooltip("Lerp factor per frame toward the target FoV. 0.1 matches the source project; lower = smoother, higher = snappier.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _zoomLerpSpeed = 0.1f;

        [Header("Movement")]
        [Tooltip("Multiplier passed to PlayerMovement.SetAimSlowdown while aiming. 0.25 = quarter speed, 0 = no movement. 1 = no slowdown (would be a no-op).")]
        [Range(0f, 1f)]
        [SerializeField] private float _aimSlowdown = 0.25f;

        private PlayerMovement _movement;
        private InputAction _aimAction;
        private bool _isAiming;

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

        private void Update()
        {
            // Drive the camera FoV. Cinemachine 3.x exposes Lens
            // directly: camera.Lens.FieldOfView (the YAML stores it
            // as m_Lens.FieldOfView in the asset, but at runtime it
            // is the LensSettings struct).
            if (_virtualCamera != null)
            {
                float targetFoV = _isAiming ? _zoomFoV : _defaultFoV;
                var lens = _virtualCamera.Lens;
                lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFoV, _zoomLerpSpeed);
                _virtualCamera.Lens = lens;
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
