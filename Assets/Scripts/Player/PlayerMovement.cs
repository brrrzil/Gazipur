using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _crouchSpeed = 2.5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _gravity = 20f;
    [SerializeField] private AudioSource _jumpSource;    

    [Header("Crouch Settings")]
    [SerializeField] private float _crouchHeight = 0.5f;
    [SerializeField] private float _standingHeight = 1f;
    [SerializeField] private float _crouchTransitionSpeed = 8f;

    [Header("Camera")]
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private float _mouseSensitivity = 100f;
    [SerializeField] private float _cameraHeightNormal = 0.8f;
    [SerializeField] private float _cameraHeightCrouch = 0.4f;

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckDistance = 0.2f;

    [Header("Fall Damage")]
    [Tooltip("Minimum fall height (in meters) before any damage is taken. Falls shorter than this deal no damage. Default 3m so a normal jump (~2m) doesn't hurt.")]
    [SerializeField] private float _fallDamageThreshold = 3f;
    [Tooltip("Damage per meter fallen BEYOND the threshold. So a 5m fall with threshold 3 and per-meter 10 deals (5-3)*10 = 20 damage.")]
    [SerializeField] private float _fallDamagePerMeter = 10f;
    [Tooltip("Sound played once when the player lands from a damaging fall. Uses _jumpSource.PlayOneShot — doesn't interrupt other sounds.")]
    [SerializeField] private AudioClip _fallSound;
    [Tooltip("Seconds of movement slowdown after a damaging fall. 0 disables the slowdown entirely.")]
    [SerializeField] private float _fallSlowdownDuration = 1f;
    [Tooltip("Movement speed multiplier during the slowdown. 0.5 = half speed, 0 = no movement. Default 0.5.")]
    [SerializeField] private float _fallSlowdownFactor = 0.5f;

    private CharacterController _controller;
    private Vector3 _velocity = Vector3.zero;
    private float _currentSpeed;
    private float _xRotation = 0f;
    private bool _isCrouching = false;
    private bool _wantsToCrouch = false;
    private float _currentCameraHeight;
    private bool _hasJumped = false;

    // Fall damage tracking (round 18).
    private bool _wasGrounded;
    private float _fallStartY;
    // Round 19: Time.time at which the post-fall slowdown ends. 0 = no
    // active slowdown. Compare against Time.time in HandleMovement.
    private float _slowdownEndTime;

    [Inject] private PlayerState _state;

    private PlayerInputActions _inputActions;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _isRunning;
    private bool _jumpPressed;

    private bool _isGrounded;
    public bool IsGrounded => _isGrounded;
    private bool _isUIMode;
    [Inject] GameModeManager _gameMode;
    [Inject]
    void Init()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerInputActions();

        _standingHeight = _controller.height;
        _currentCameraHeight = _cameraHeightNormal;

        _gameMode.onChangeMode += SetMode;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false; // �������� ������

        // ���������� ������
        _xRotation = 0f;
        _cameraHolder.localRotation = Quaternion.Euler(0f, 0f, 0f);

        UpdateCameraPosition();
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();

        _inputActions.Player.Jump.performed += OnJumpPerformed;
        _inputActions.Player.Jump.canceled += OnJumpCanceled;

        _inputActions.Player.Crouch.performed += OnCrouchPerformed;
        _inputActions.Player.Crouch.canceled += OnCrouchCanceled;

        _inputActions.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _inputActions.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        _inputActions.Player.Look.performed += ctx => _lookInput = ctx.ReadValue<Vector2>();
        _inputActions.Player.Look.canceled += ctx => _lookInput = Vector2.zero;

        _inputActions.Player.Run.performed += ctx => _isRunning = true;
        _inputActions.Player.Run.canceled += ctx => _isRunning = false;
    }

    private void OnDisable()
    {
        _inputActions.Player.Jump.performed -= OnJumpPerformed;
        _inputActions.Player.Jump.canceled -= OnJumpCanceled;
        _inputActions.Player.Crouch.performed -= OnCrouchPerformed;
        _inputActions.Player.Crouch.canceled -= OnCrouchCanceled;

        _inputActions.Player.Disable();
    }

    void Update()
    {
        // BUGFIX (round 13): enforce cursor visibility every frame based on
        // the current _isUIMode flag. The previous behaviour was to set the
        // cursor only in SetMode (which runs once on onChangeMode). If
        // anything else afterwards flipped Cursor.visible=true — a panel's
        // OnEnable, an EventSystem quirk, a third-party package — the
        // cursor stayed visible after the player exited a UI mode via Esc.
        // The defensive hide in GameModeManager's OnEsc handler wasn't
        // enough because other code can run AFTER the handler. Enforce
        // every frame as a final guarantee.
        EnforceCursorState();

        if (_isUIMode) return;
        HandleCrouch();
        ApplyGravity();
        HandleMovement();
        HandleJump();
        HandleCameraRotation();
        UpdateCameraPosition();

        _isGrounded = CheckIfGrounded();

        // BUGFIX (round 18): detect fall and apply damage on landing.
        // Runs after _isGrounded is updated so we can compare to the
        // previous frame's state.
        HandleFallDamage();
    }

    private void EnforceCursorState()
    {
        if (_isUIMode)
        {
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else
        {
            if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void HandleCameraRotation()
    {
        float mouseX = _lookInput.x * _mouseSensitivity / 100;
        float mouseY = _lookInput.y * _mouseSensitivity / 100;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        _cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    bool CheckIfGrounded()
    {
        float rayLength = (_controller.height / 2) + _groundCheckDistance;
        RaycastHit hit;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayLength))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle <= _controller.slopeLimit)
                return true;
        }
        return false;
    }

    bool CanStandUp()
    {
        float checkDistance = _standingHeight - _controller.height;
        if (checkDistance <= 0.05f) return true;

        Vector3 checkStart = transform.position + Vector3.up * _controller.height;
        return !Physics.Raycast(checkStart, Vector3.up, checkDistance);
    }

    void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        _wantsToCrouch = true;
    }

    void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        _wantsToCrouch = false;
    }

    void HandleCrouch()
    {
        float targetHeight;
        float targetCameraHeight;

        if (_wantsToCrouch)
        {
            targetHeight = _crouchHeight;
            targetCameraHeight = _cameraHeightCrouch;
            _isCrouching = true;
        }
        else
        {
            if (CanStandUp())
            {
                targetHeight = _standingHeight;
                targetCameraHeight = _cameraHeightNormal;
                _isCrouching = false;
            }
            else
            {
                targetHeight = _crouchHeight;
                targetCameraHeight = _cameraHeightCrouch;
                _isCrouching = true;
            }
        }

        float newHeight = Mathf.Lerp(_controller.height, targetHeight, _crouchTransitionSpeed * Time.deltaTime);
        _controller.height = newHeight;

        _currentCameraHeight = Mathf.Lerp(_currentCameraHeight, targetCameraHeight, _crouchTransitionSpeed * Time.deltaTime);

        AdjustPositionToGround();
    }

    void AdjustPositionToGround()
    {
        RaycastHit hit;
        float checkDistance = _controller.height / 2 + 0.1f;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, checkDistance))
        {
            Vector3 newPos = transform.position;
            float targetY = hit.point.y + (_controller.height / 2);

            if (Mathf.Abs(newPos.y - targetY) > 0.01f)
            {
                newPos.y = targetY;
                transform.position = newPos;
            }
        }
    }

    void UpdateCameraPosition()
    {
        if (_cameraHolder == null) return;

        Vector3 cameraPos = _cameraHolder.localPosition;
        cameraPos.y = _currentCameraHeight;
        _cameraHolder.localPosition = cameraPos;
    }

    void HandleMovement()
    {
        float horizontal = _moveInput.x;
        float vertical = _moveInput.y;

        if (_isCrouching)
            _currentSpeed = _crouchSpeed;
        else if (_isRunning)
            _currentSpeed = _runSpeed;
        else
            _currentSpeed = _walkSpeed;

        // Round 19: post-fall slowdown. While _slowdownEndTime is in the
        // future, scale the chosen speed down by _fallSlowdownFactor.
        // Camera look is NOT slowed — the player can still rotate freely.
        if (Time.time < _slowdownEndTime && _fallSlowdownFactor < 1f)
            _currentSpeed *= _fallSlowdownFactor;

        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection.y = 0;
        moveDirection.Normalize();

        Vector3 movement = moveDirection * _currentSpeed * Time.deltaTime;
        _controller.Move(movement);
    }

    void OnJumpPerformed(InputAction.CallbackContext context)
    {
        _jumpPressed = true;
    }

    void OnJumpCanceled(InputAction.CallbackContext context)
    {
        _jumpPressed = false;
    }

    void HandleJump()
    {
        if (_jumpPressed && !_isCrouching && CheckIfGrounded() && !_hasJumped)
        {
            _velocity.y = Mathf.Sqrt(_jumpHeight * 2f * _gravity);
            _hasJumped = true;
            // NOTE: jump sound moved to landing (see HandleLanding) per design fix.
        }

        if (CheckIfGrounded() && _velocity.y <= 0)
        {
            if (_hasJumped)
            {
                // First frame after touchdown following a jump: play the landing sound.
                _hasJumped = false;
                if (_jumpSource != null && _jumpSource.clip != null)
                    _jumpSource.Play();
            }
        }
    }

    void ApplyGravity()
    {
        _velocity.y -= _gravity * Time.deltaTime;
        Vector3 verticalMove = new Vector3(0, _velocity.y, 0) * Time.deltaTime;
        _controller.Move(verticalMove);
    }

    // BUGFIX (round 18): fall damage. Detects transitions between grounded
    // and airborne. When the player lands after falling from above the
    // threshold, applies damage scaled by excess distance and plays the
    // configured fall sound.
    //
    // Edge cases:
    //  - Normal jump (~_jumpHeight=2m) is below _fallDamageThreshold=3m,
    //    so jumping doesn't hurt.
    //  - Walking down a slope: player stays grounded, no fall registered.
    //  - Teleport: fallStartY stays at the last grounded position; if the
    //    next frame is also grounded, no fall is registered. Acceptable.
    //  - Death/die mode: Update() still runs (we don't early-out unless
    //    _isUIMode), so a fall while dying would still register. PlayerState
    //    handles health <= 0 separately.
    void HandleFallDamage()
    {
        if (_wasGrounded && !_isGrounded)
        {
            // Just left the ground — mark the start of the fall.
            _fallStartY = transform.position.y;
        }
        else if (!_wasGrounded && _isGrounded)
        {
            // Just landed — compute fall distance from the highest point
            // we recorded during the airborne phase.
            float fallDistance = _fallStartY - transform.position.y;
            if (fallDistance > _fallDamageThreshold)
            {
                float excess = fallDistance - _fallDamageThreshold;
                int damage = Mathf.RoundToInt(excess * _fallDamagePerMeter);
                if (_state != null)
                    _state.TakeDamage(damage);
                if (_fallSound != null && _jumpSource != null)
                    _jumpSource.PlayOneShot(_fallSound);
                // Round 19: arm the post-fall slowdown. HandleMovement
                // checks _slowdownEndTime each frame and multiplies the
                // chosen speed by _fallSlowdownFactor while active.
                if (_fallSlowdownDuration > 0f && _fallSlowdownFactor < 1f)
                    _slowdownEndTime = Time.time + _fallSlowdownDuration;
            }
        }
        _wasGrounded = _isGrounded;
    }
    private void SetMode(EnumData.GameMode mode)
    {
        // Use the explicit IsUIMode helper from GameModeManager so that any
        // future UI mode added to the enum is automatically considered a UI
        // mode (just add it to the UIModes set in GameModeManager.cs).
        _isUIMode = GameModeManager.IsUIMode(mode);
        if (_isUIMode)
        {
            // UI modes (inventory, trade, dialog, die, win, etc.):
            // release cursor so the player can click UI.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Back in the game world: hide and lock cursor, return control.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}