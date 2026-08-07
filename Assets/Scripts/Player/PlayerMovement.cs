using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _gravity = 20f;
    [SerializeField] private AudioSource _jumpSource;

    [Header("Crouch")]
    [Tooltip("CharacterController height when crouched (half of the standing height for a 50% shrink).")]
    [SerializeField] private float _crouchHeight = 1f;
    [Tooltip("How much lower the camera goes when crouched, in meters.")]
    [SerializeField] private float _crouchCameraDrop = 0.5f;
    [Tooltip("Lerp speed for the crouch/stand transition (higher = snappier).")]
    [SerializeField] private float _crouchTransitionSpeed = 8f;
    [Tooltip("Movement speed while crouched.")]
    [SerializeField] private float _crouchSpeed = 2.5f;

    [Header("Camera")]
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private float _mouseSensitivity = 100f;
    [Tooltip("Camera local Y above the Player root when standing.")]
    [SerializeField] private float _cameraHeightNormal = 0.8f;

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckDistance = 0.2f;

    [Header("Fall Damage")]
    [Tooltip("Minimum fall height (m) before damage. Jumps (~2m) do not hurt.")]
    [SerializeField] private float _fallDamageThreshold = 3f;
    [Tooltip("Damage per meter beyond the threshold.")]
    [SerializeField] private float _fallDamagePerMeter = 10f;
    [SerializeField] private AudioClip _fallSound;
    [Tooltip("Seconds of movement slowdown after a damaging fall. 0 disables.")]
    [SerializeField] private float _fallSlowdownDuration = 1f;
    [Tooltip("Speed multiplier during the slowdown (0.5 = half speed).")]
    [SerializeField] private float _fallSlowdownFactor = 0.5f;

    [Header("Animation")]
    [Tooltip("Animator on the player's legs/hands rig. Writes isRun/isWalk/isCrouch bools each frame they change. Optional - null guard skips the write if not bound.")]
    [SerializeField] private Animator _legsHandsAnimator;

    private CharacterController _controller;
    private float _standingHeight;
    private float _currentCameraHeight;
    private float _xRotation;
    private bool _isCrouching;
    private bool _wantsToCrouch;
    private bool _isReversing;
    private bool _wasRun;
    private bool _wasWalk;
    private bool _isGrounded;
    private bool _isUIMode;
    private bool _isRunning;
    private bool _jumpPressed;
    private bool _hasJumped;
    private bool _wasGrounded;
    private bool _isLocked;
    private float _lockEndTime;
    private float _fallStartY;
    private float _slowdownEndTime;
    private float _aimSlowdown = 1f;
    private float _currentSpeed;
    private Vector3 _velocity;
    private PlayerInputActions _inputActions;
    private Vector2 _moveInput;
    private Vector2 _lookInput;

    [Inject] private PlayerState _state;
    [Inject] private GameModeManager _gameMode;

    public bool IsGrounded => _isGrounded;

    [Inject]
    void Init()
    {
        _controller = GetComponent<CharacterController>();
        if (_controller != null) _standingHeight = _controller.height;
        _inputActions = new PlayerInputActions();

        _currentCameraHeight = _cameraHeightNormal;

        _gameMode.onChangeMode += SetMode;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
        EnforceCursorState();

        if (_isUIMode) return;
        if (_isLocked && Time.time >= _lockEndTime) _isLocked = false;
        if (_isLocked) return;

        HandleCrouch();
        ApplyGravity();
        HandleMovement();
        HandleJump();
        HandleCameraRotation();
        UpdateCameraPosition();

        _isGrounded = CheckIfGrounded();
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
        _xRotation = Mathf.Clamp(_xRotation, -80f, 70f);

        _cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    bool CheckIfGrounded()
    {
        float rayLength = (_controller.height / 2) + _groundCheckDistance;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            return slopeAngle <= _controller.slopeLimit;
        }
        return false;
    }

    void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        _wantsToCrouch = true;
        if (_legsHandsAnimator != null)
        {
            _isReversing = false;
            _legsHandsAnimator.SetFloat("crouchDirection", 1f);
            _legsHandsAnimator.SetBool("isCrouch", true);
            _legsHandsAnimator.Play("Isha_Crouch", 0, 0f);
        }
    }

    void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        _wantsToCrouch = false;
        if (_legsHandsAnimator != null)
        {
            var s = _legsHandsAnimator.GetCurrentAnimatorStateInfo(0);
            if (s.IsName("Isha_Crouch"))
            {
                _isReversing = true;
                _legsHandsAnimator.SetFloat("crouchDirection", -1f);
                float t = s.normalizedTime;
                if (t > 1f) t -= Mathf.Floor(t);
                _legsHandsAnimator.Play("Isha_Crouch", 0, t);
            }
        }
    }

    void HandleCrouch()
    {
        float targetHeight = _wantsToCrouch ? _crouchHeight : _standingHeight;
        float targetCameraY = _wantsToCrouch ? _cameraHeightNormal - _crouchCameraDrop : _cameraHeightNormal;
        _isCrouching = _wantsToCrouch;

        if (_controller != null)
            _controller.height = Mathf.Lerp(_controller.height, targetHeight, _crouchTransitionSpeed * Time.deltaTime);
        _currentCameraHeight = Mathf.Lerp(_currentCameraHeight, targetCameraY, _crouchTransitionSpeed * Time.deltaTime);
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
        if (_isCrouching)
            _currentSpeed = _crouchSpeed;
        else if (_isRunning)
            _currentSpeed = _runSpeed;
        else
            _currentSpeed = _walkSpeed;

        if (Time.time < _slowdownEndTime && _fallSlowdownFactor < 1f)
            _currentSpeed *= _fallSlowdownFactor;
        if (_aimSlowdown < 1f)
            _currentSpeed *= _aimSlowdown;

        Vector3 moveDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        moveDirection.y = 0;
        moveDirection.Normalize();

        _controller.Move(moveDirection * _currentSpeed * Time.deltaTime);

        if (_legsHandsAnimator != null)
        {
            bool isMoving = _moveInput.sqrMagnitude > 0.01f;
            bool isRun = _isRunning && !_isCrouching && isMoving;
            bool isWalk = isMoving && !_isRunning && !_isCrouching;

            if (isRun != _wasRun)
            {
                _wasRun = isRun;
                _legsHandsAnimator.SetBool("isRun", isRun);
            }
            if (isWalk != _wasWalk)
            {
                _wasWalk = isWalk;
                _legsHandsAnimator.SetBool("isWalk", isWalk);
            }
            if (_isReversing)
            {
                var stateInfo = _legsHandsAnimator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Isha_Crouch") && stateInfo.normalizedTime <= 0.01f)
                {
                    _legsHandsAnimator.SetBool("isCrouch", false);
                    _legsHandsAnimator.SetFloat("crouchDirection", 0f);
                    _isReversing = false;
                }
            }
        }
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
        }

        if (CheckIfGrounded() && _velocity.y <= 0 && _hasJumped)
        {
            _hasJumped = false;
            if (_jumpSource != null && _jumpSource.clip != null)
                _jumpSource.Play();
        }
    }

    void ApplyGravity()
    {
        _velocity.y -= _gravity * Time.deltaTime;
        _controller.Move(new Vector3(0, _velocity.y, 0) * Time.deltaTime);
    }

    void HandleFallDamage()
    {
        if (_wasGrounded && !_isGrounded)
        {
            _fallStartY = transform.position.y;
        }
        else if (!_wasGrounded && _isGrounded)
        {
            float fallDistance = _fallStartY - transform.position.y;
            if (fallDistance > _fallDamageThreshold)
            {
                float damage = Mathf.RoundToInt((fallDistance - _fallDamageThreshold) * _fallDamagePerMeter);
                if (_state != null) _state.TakeDamage(damage);
                if (_fallSound != null && _jumpSource != null)
                    _jumpSource.PlayOneShot(_fallSound);
                if (_fallSlowdownDuration > 0f && _fallSlowdownFactor < 1f)
                    _slowdownEndTime = Time.time + _fallSlowdownDuration;
            }
        }
        _wasGrounded = _isGrounded;
    }

    private void SetMode(EnumData.GameMode mode)
    {
        _isUIMode = GameModeManager.IsUIMode(mode);
        if (_isUIMode)
        {
            ForceIdle();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SetAimSlowdown(float value)
    {
        _aimSlowdown = Mathf.Clamp(value, 0f, 1f);
    }

    public void PlayLockedAnimation(string trigger, float duration)
    {
        if (_legsHandsAnimator != null) _legsHandsAnimator.SetTrigger(trigger);
        if (_wasRun)  { _legsHandsAnimator.SetBool("isRun", false);  _wasRun = false; }
        if (_wasWalk) { _legsHandsAnimator.SetBool("isWalk", false); _wasWalk = false; }
        _isLocked = true;
        _lockEndTime = Time.time + duration;
    }

    public void RefreshLock(string trigger, float duration)
    {
        if (_legsHandsAnimator != null) _legsHandsAnimator.SetTrigger(trigger);
        _lockEndTime = Time.time + duration;
    }

    public void UnlockAnimation(string trigger)
    {
        if (_legsHandsAnimator != null) _legsHandsAnimator.ResetTrigger(trigger);
        _isLocked = false;
    }

    public void KeepLockAlive(float extraSeconds)
    {
        if (!_isLocked) return;
        _lockEndTime = Time.time + extraSeconds;
    }

    public void ForceIdle()
    {
        if (_legsHandsAnimator != null)
        {
            _legsHandsAnimator.Play("Isha_Idle", 0, 0f);
            _legsHandsAnimator.Play("Idle_Upper", 1, 0f);
            _legsHandsAnimator.SetBool("isRun", false);
            _legsHandsAnimator.SetBool("isWalk", false);
            _legsHandsAnimator.SetBool("isCrouch", false);
        }
        _isLocked = false;
        _wasRun = false;
        _wasWalk = false;
        _isCrouching = false;
        _wantsToCrouch = false;
        _isReversing = false;
    }
}
