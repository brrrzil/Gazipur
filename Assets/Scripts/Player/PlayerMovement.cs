using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _crouchSpeed = 2.5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _gravity = 20f;

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

    private CharacterController _controller;
    private Vector3 _velocity = Vector3.zero;
    private float _currentSpeed;
    private float _xRotation = 0f;
    private bool _isCrouching = false;
    private bool _wantsToCrouch = false;
    private float _currentCameraHeight;
    private bool _hasJumped = false;

    private PlayerInputActions _inputActions;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _isRunning;
    private bool _jumpPressed;

    private bool _isGrounded;
    public bool IsGrounded => _isGrounded;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new PlayerInputActions();

        _standingHeight = _controller.height;
        _currentCameraHeight = _cameraHeightNormal;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
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
        HandleCrouch();
        ApplyGravity();
        HandleMovement();
        HandleJump();
        HandleCameraRotation();
        UpdateCameraPosition();

        _isGrounded = CheckIfGrounded();
    }

    void HandleCameraRotation()
    {
        float mouseX = _lookInput.x * _mouseSensitivity * Time.deltaTime;
        float mouseY = _lookInput.y * _mouseSensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        _cameraHolder.localRotation = Quaternion.Euler(_xRotation, _cameraHolder.localEulerAngles.y + mouseX, 0f);
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
        float horizontal = _moveInput.x / 10;
        float vertical = _moveInput.y / 10;

        if (_isCrouching)
            _currentSpeed = _crouchSpeed;
        else if (_isRunning)
            _currentSpeed = _runSpeed;
        else
            _currentSpeed = _walkSpeed;

        Vector3 moveDirection = _cameraHolder.right * horizontal + _cameraHolder.forward * vertical;
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
        }

        if (CheckIfGrounded() && _velocity.y <= 0)
        {
            _hasJumped = false;
        }
    }

    void ApplyGravity()
    {
        _velocity.y -= _gravity * Time.deltaTime;
        Vector3 verticalMove = new Vector3(0, _velocity.y, 0) * Time.deltaTime;
        _controller.Move(verticalMove);
    }
}