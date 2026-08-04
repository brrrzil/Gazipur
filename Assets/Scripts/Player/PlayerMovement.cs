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

    [Header("Animation")]
    [Tooltip("Animator on the player's legs/hands rig (Isha_GamePlay). PlayerMovement writes three bools to it (isRun, isWalk, isCrouch) every frame they change. Optional - null guard skips the write if not bound.")]
    [SerializeField] private Animator _legsHandsAnimator;

    private CharacterController _controller;
    private Vector3 _velocity = Vector3.zero;
    private float _currentSpeed;
    private float _xRotation = 0f;
    private bool _isCrouching = false;
    private bool _wantsToCrouch = false;
    private float _currentCameraHeight;
    private bool _hasJumped = false;
    // (round 57) Multiplier pushed in by AimController every frame
    // while right mouse is held. 1f = no slowdown, 0.25f = quarter
    // speed. Default 1f so a scene without an AimController behaves
    // exactly as before.
    private float _aimSlowdown = 1f;

    // Fall damage tracking (round 18).
    private bool _wasGrounded;
    private float _fallStartY;
    // Round 19: Time.time at which the post-fall slowdown ends. 0 = no
    // active slowdown. Compare against Time.time in HandleMovement.
    private float _slowdownEndTime;

    [Inject] private PlayerState _state;
    private bool _isLocked;
    private float _lockEndTime;

    private PlayerInputActions _inputActions;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _isRunning;
    private bool _jumpPressed;

    private bool _isGrounded;
    public bool IsGrounded => _isGrounded;
    private bool _isUIMode;
    // Round 67: per-bool caches for the legs/hands animator. The
    // Isha_Legs_Hands controller listens to three Bool parameters
    // (isRun, isWalk, isCrouch) and resolves them to a state with
    // the priority Crouch > Run > Walk > Idle. We only push a value
    // to the Animator when it actually changes, so we keep the
    // last-pushed value of each bool here and diff against the
    // current frame's intended value.
    private bool _wasRun;
    private bool _wasWalk;
    // Round 84 v4: persistent flag set
    // true at the moment of release
    // and cleared when the reverse
    // playback reaches the start
    // of the clip. The Update
    // check fires on this flag
    // (not on the _wantsToCrouch
    // check alone) so that the
    // 'first-press-then-release'
    // case (a tap that did not
    // advance the forward pass
    // past the very first
    // frame) still gets a
    // reliable 'exit to Idle'
    // signal when the reverse
    // pass has finished.
    private bool _isReversing;
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
        if (_isLocked && Time.time >= _lockEndTime) _isLocked = false;
        if (_isLocked) return;
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
        _xRotation = Mathf.Clamp(_xRotation, -80f, 70f);

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
        // Round 84: drive the crouch animation
        // forward (1.0) on press. The
        // 'Isha_Crouch' Animator state has
        // m_SpeedParameter set to
        // 'crouchDirection' and
        // m_SpeedParameterActive = 1, so
        // setting this float to 1.0 makes
        // the state play at full speed
        // forward. The state itself is
        // non-looping (the .anim file is
        // 'Run.anim' in the Isha folder,
        // bound to the Isha_Crouch state in
        // the Isha_Animator.controller, and
        // 'Run.anim' has its own non-loop
        // behaviour - the Animator plays the
        // motion once and freezes at the end
        // while crouchDirection stays at
        // 1.0). The isCrouch bool is set to
        // true at the same time so the
        // 'Idle -> Isha_Crouch' transition
        // (Animator condition
        // 'isCrouch == true') fires and
        // switches the Animator into the
        // Isha_Crouch state.
        if (_legsHandsAnimator != null)
        {
            _isReversing = false;
            _legsHandsAnimator.SetFloat("crouchDirection", 1f);
            _legsHandsAnimator.SetBool("isCrouch", true);
            // Round 84 v4: explicit Play at
            // normalizedTime=0 on press.
            // The Animator transition
            // 'Idle -> Isha_Crouch' fires
            // when isCrouch goes true, but
            // the transition does not
            // reset the target state's
            // normalizedTime to 0 on its
            // own - the playback head
            // resumes from wherever the
            // previous Isha_Crouch visit
            // left it (or, on a brand new
            // game, from 0). Without the
            // explicit Play() call here,
            // a second press after a long
            // reverse-walk that ended at
            // normalizedTime 0.1 would
            // resume the new forward pass
            // from 0.1 (continuing the
            // previous play) instead of
            // restarting cleanly from 0.
            // The Play(state, 0, 0f) call
            // forces a clean restart.
            _legsHandsAnimator.Play("Isha_Crouch", 0, 0f);
        }
    }

    void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        _wantsToCrouch = false;
        // Round 84 v3: on release, drive
        // the crouch animation backward
        // (-1.0) AND keep the playback
        // head at whatever frame the
        // forward pass had reached when
        // the user released the key. The
        // user is explicit about this in
        // round 84 v3: 'on release, the
        // animation plays in reverse
        // from the same frame where it
        // was at the moment of release'.
        // Concretely:
        //   1. Read the current
        //      normalizedTime of the
        //      Isha_Crouch state
        //      (this is the frame the
        //      user saw when they
        //      released the key).
        //   2. Set the crouchDirection
        //      float to -1f so the
        //      state's m_Speed becomes
        //      -1 (m_Speed 1 * parameter
        //      -1 = -1).
        //   3. Call Animator.Play with
        //      the read-back
        //      normalizedTime so the
        //      Animator state machine
        //      acknowledges the
        //      position reset and
        //      starts stepping
        //      backward from there. A
        //      plain SetFloat without
        //      Play is not enough
        //      because the Animator
        //      does not always re-evaluate
        //      the state machine just
        //      because one of its speed
        //      parameters changed - it
        //      only re-evaluates on
        //      Play, on a transition
        //      being met, or on the
        //      next state update tick,
        //      and a speed flip from
        //      +1 to -1 is a 'negative
        //      speed' that needs an
        //      explicit nudge to be
        //      picked up.
        // The fractional part of
        // normalizedTime is used (%
        // 1f) because the Isha_Crouch
        // state is set to m_LoopTime: 1
        // (added in round 84 v2), so a
        // user who holds the crouch key
        // past the end of the clip will
        // have a normalizedTime > 1
        // (the clip wrapped). The
        // reverse-completion check in
        // Update still fires on
        // 'normalizedTime <= 0.01f',
        // so a wrapped user gets
        // 'reverse from the wrap
        // point' which is visually
        // 'reverse from the end of
        // the clip' (the loop seam).
        // The isCrouch bool is NOT
        // set to false here - the
        // Animator must STAY in
        // Isha_Crouch for the
        // reverse playback to be
        // visible. The bool is reset
        // to false in Update when
        // the reverse reaches the
        // start of the clip.
        if (_legsHandsAnimator != null)
        {
            var s = _legsHandsAnimator.GetCurrentAnimatorStateInfo(0);
            if (s.IsName("Isha_Crouch"))
            {
                // Read the frame the user
                // saw at release. With
                // m_LoopTime: 0 in round
                // 84 v4, normalizedTime
                // is always in [0, 1]
                // (non-loop state never
                // wraps), but the
                // mod-1f guard is still
                // there for safety in
                // case a future Editor
                // change re-enables
                // m_LoopTime.
                float currentTime = s.normalizedTime;
                if (currentTime > 1f) currentTime = currentTime - Mathf.Floor(currentTime);
                // Set _isReversing BEFORE
                // the SetFloat so the
                // Update check below
                // (which runs on the
                // next frame) can see
                // the flag and not
                // fire on a press.
                _isReversing = true;
                // SetFloat first so the
                // m_Speed = -1 is in
                // effect when Play()
                // restarts the state
                // machine on the
                // current frame.
                _legsHandsAnimator.SetFloat("crouchDirection", -1f);
                _legsHandsAnimator.Play("Isha_Crouch", 0, currentTime);
            }
        }
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
        // (round 57) Aim slowdown. AimController.SetAimSlowdown pushes
        // a multiplier into _aimSlowdown every frame while right
        // mouse is held. 1f = no slowdown, 0.25f = quarter speed.
        // Camera look is NOT slowed — only translation.
        if (_aimSlowdown < 1f)
            _currentSpeed *= _aimSlowdown;

        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection.y = 0;
        moveDirection.Normalize();

        Vector3 movement = moveDirection * _currentSpeed * Time.deltaTime;
        _controller.Move(movement);

        // Round 69: drive the legs/hands animator with three bools.
        // The Isha_Legs_Hands controller (Assets/Animations/Isha/
        // Isha_Legs_Hands.controller) listens to isRun, isWalk and
        // isCrouch and resolves them to one of Isha_Idle / Isha_Walk
        // / Isha_Run / Isha_Crouch with the priority
        //   Crouch > Run > Walk > Idle
        // (each state's incoming transition is 'this bool true,
        // the other two false'). So we set:
        //   isCrouch  = _isCrouching && isMoving
        //   isRun     = _isRunning && !_isCrouching && isMoving
        //   isWalk    = isMoving && !_isRunning && !_isCrouching
        // and rely on the Animator's transitions to pick the right
        // state. We only push to SetBool on a change so the Animator
        // does not receive a redundant write every frame.
        //
        // Round 68 fix: isCrouch now requires BOTH _isCrouching and
        // isMoving. Without the isMoving clause, simply holding the
        // crouch key (C) with no WASD pressed sent the animator into
        // Isha_Crouch at m_Speed=0.3, so the legs/hands kept 'running
        // on the spot' while the player was standing still in a crouch.
        // With the extra && isMoving, holding C without moving falls
        // through to the all-false state and the animator goes to
        // Isha_Idle (m_Speed=0, m_CycleOffset=0.1) - the same frozen
        // pose used when standing still. As soon as the player presses
        // any movement key while crouched, isMoving becomes true,
        // isCrouch flips to true, and the controller transitions into
        // Isha_Crouch as before.
        //
        // Round 69 fix: isRun gets the same treatment. Holding Shift
        // without any WASD pressed also dropped the player into
        // Isha_Run (m_Speed=0.7) with the rig animating in place.
        // Adding && isMoving to the isRun expression makes Shift a
        // no-op for the animator unless the player is also producing
        // movement input. As soon as the player presses WASD, the rig
        // transitions into Isha_Run. When the player releases WASD but
        // keeps Shift held, the rig falls back to Isha_Idle (frozen).
        if (_legsHandsAnimator != null)
        {
            // Round 84: 'isCrouch' is no
            // longer driven from Update().
            // The crouch animation is now
            // press-driven (forward on press,
            // reverse on release), not
            // state-driven, so the per-frame
            // SetBool('isCrouch', ...)
            // pattern that the round 67-69
            // code path used is removed. The
            // isCrouch bool is still set by
            // OnCrouchPerformed / Update's
            // reverse-completion check (so
            // the 'Idle -> Isha_Crouch' and
            // 'Isha_Crouch -> Isha_Idle'
            // transitions in the Animator
            // still fire), but it is not
            // recomputed every frame from
            // '_isCrouching && isMoving' any
            // more. The 'isMoving' guard the
            // user asked to drop was
            // specifically about the
            // animation, not about the
            // character height / camera
            // height (those still go through
            // the HandleCrouch() method
            // below, which uses _wantsToCrouch
            // and is independent of
            // isMoving). The isRun and
            // isWalk bools are still driven
            // from Update() with their
            // isMoving guards - only the
            // crouch bool drops the guard
            // because the crouch animation
            // is no longer 'is the player
            // crouching AND moving' but
            // rather 'is the crouch key
            // currently held', which is
            // exactly what _wantsToCrouch
            // tracks and what the new
            // OnCrouchPerformed /
            // OnCrouchCanceled handlers
            // drive into the Animator.
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
            // Round 84: crouch reverse
            // completion check. When the
            // user releases the crouch
            // key, OnCrouchCanceled sets
            // 'crouchDirection' to -1.0
            // and the Isha_Crouch state
            // starts playing in reverse.
            // When the reverse playback
            // reaches the start of the
            // clip (normalizedTime <=
            // 0.01f, with a small margin
            // to avoid floating-point
            // precision issues at exactly
            // 0), we set the isCrouch bool
            // to false. This fires the
            // 'Isha_Crouch -> Isha_Idle'
            // transition (which the
            // round 67 / 68 / 69 state
            // machine is set up for: it
            // already requires isCrouch
            // = 0 AND isRun = 0 AND
            // isWalk = 0 to fire from
            // Isha_Crouch to Isha_Idle),
            // and the rig returns to
            // standing. The reverse
            // playback is what the user
            // asked for in round 84:
            // 'when the player releases
            // the crouch key, the
            // animation should play in
            // reverse'.
            //
            // The '!_wantsToCrouch' guard
            // is critical: the
            // normalizedTime <= 0.01f
            // check would also be true
            // at the very START of a
            // forward playback (the
            // normalizedTime is 0 at
            // t=0), which would
            // immediately re-trigger
            // the Isha_Crouch -> Isha_Idle
            // transition and snap the
            // rig out of Isha_Crouch on
            // the first press. Gating
            // the check on
            // '!_wantsToCrouch' (i.e.
            // 'the user has released
            // the crouch key') makes
            // sure the exit only fires
            // after a release-and-reverse
            // cycle, not on a fresh
            // press.
            // Round 84 v4: exit on
            // _isReversing (not on
            // '!_wantsToCrouch' alone)
            // so the 'first press
            // then release' case (a
            // tap that did not
            // advance the forward
            // pass past the very
            // first frame) still
            // gets a reliable exit
            // signal. The
            // _isReversing flag is
            // set in
            // OnCrouchCanceled and
            // cleared here when the
            // reverse pass has
            // completed.
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

    // (round 57) AimController calls this every frame while right
    // mouse is held, with the configured slowdown value. When the
    // button is released AimController calls with 1f to restore
    // normal speed. Value is clamped so a misconfigured 1.5f or
    // negative value cannot make the player faster than normal.
    public void SetAimSlowdown(float value)
    {
        _aimSlowdown = Mathf.Clamp(value, 0f, 1f);
    }

    // Plays a one-shot player animation and locks movement for
    // 'duration' seconds. Called by InteractObject subclasses
    // (GarbageObject, DangerGarbageObject, etc) via the
    // PlayInteractAnimation() helper in the base class. While
    // locked, Update() returns early before HandleMovement, so
    // the player cannot walk away mid-grab. The run/walk bools
    // are also cleared so the rig does not stay in Isha_Run
    // after the lock ends (the Animator state would otherwise
    // keep playing until the next transition fires).
    public void PlayLockedAnimation(string trigger, float duration)
    {
        if (_legsHandsAnimator != null) _legsHandsAnimator.SetTrigger(trigger);
        if (_wasRun)  { _legsHandsAnimator.SetBool("isRun", false);  _wasRun = false; }
        if (_wasWalk) { _legsHandsAnimator.SetBool("isWalk", false); _wasWalk = false; }
        _isLocked = true;
        _lockEndTime = Time.time + duration;
    }

    // Extends an existing lock by 'duration' more seconds and
    // re-fires the trigger. Used by GarbageObject's loop pickup
    // (PicItem fires after every hold bar complete; we extend
    // the lock so the player stays standing for the next pick).
    public void RefreshLock(string trigger, float duration)
    {
        if (_legsHandsAnimator != null) _legsHandsAnimator.SetTrigger(trigger);
        _lockEndTime = Time.time + duration;
    }

    // Drops the lock immediately and resets the trigger so the
    // animation stops looping. Used when the player releases E
    // or the interaction is cancelled.
    public void UnlockAnimation(string trigger)
    {
        if (_legsHandsAnimator != null) _legsHandsAnimator.ResetTrigger(trigger);
        _isLocked = false;
    }
}