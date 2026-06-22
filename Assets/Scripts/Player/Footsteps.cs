using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]

public class Footsteps : MonoBehaviour
{
    [Header("���� �� ������")]
    public AudioClip footstepsLitter;
    public float stepDurationLitter = 0.4f;
    public int totalStepsLitter = 20;

    [Header("���� �� �����")]
    public AudioClip footstepsDirt;
    public float stepDurationDirt = 0.4f;
    public int totalStepsDirt = 20;

    [Header("���� �� ����")]
    public AudioClip footstepsWater;
    public float stepDurationWater = 0.4f;
    public int totalStepsWater = 8;

    [Header("����������� ��������� ")]
    public AudioClip footstepsOld;
    public float stepDurationOld = 0.4f;
    public int totalStepsOld = 20;

    [Header("��������� ��������")]
    [Tooltip("Seconds between steps while walking. Hard-coded per speed state (see HandleFootsteps).")]
    public float walkingStepInterval = 0.55f;
    [Tooltip("Seconds between steps while running. Hard-coded per speed state (see HandleFootsteps).")]
    public float runningStepInterval = 0.28f;
    [Tooltip("Below this world-space speed the player is considered idle (no footsteps).")]
    public float minSpeedForSteps = 0.1f;
    public float walkSpeed = 4f;
    public float runSpeed = 8f;

    [Header("����������� �����������")]
    public float groundCheckDistance = 1.5f;
    public LayerMask groundLayerMask = ~0;

    [Header("�����������")]
    public bool randomizePitch = true;
    public float pitchRange = 0.1f;

    private CharacterController controller;
    private AudioSource audioSource;
    private float nextStepTime;
    private bool isMoving;
    private SurfaceType currentSurface;

    private AudioClip currentClip;
    private float currentStepDuration;
    private int currentTotalSteps;
    private float currentStepInterval;

    private bool isPlayingStep = false;
    private float stepEndTime;

    // Computed once per Update, consumed by CheckMovement + HandleFootsteps.
    private float currentSpeed;

    // Input System ����������
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    private bool isRunning;

    // ��� �������� ���������� �� ����� (����� ������� � PlayerMovement)
    private bool isGrounded = true;

    private enum SurfaceType
    {
        Indoor,
        Ground,
        Wet,
        Unknown
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        inputActions = new PlayerInputActions();
    }

    void Start()
    {
        audioSource.loop = false;
        audioSource.playOnAwake = false;

        currentSurface = SurfaceType.Unknown;
        UpdateSurfaceSettings();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Run.performed += ctx => isRunning = true;
        inputActions.Player.Run.canceled += ctx => isRunning = false;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled -= ctx => moveInput = Vector2.zero;
        inputActions.Player.Run.performed -= ctx => isRunning = true;
        inputActions.Player.Run.canceled -= ctx => isRunning = false;

        inputActions.Player.Disable();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
        isPlayingStep = false;
    }

    void Update()
    {
        CheckSurface();
        ComputeCurrentSpeed();
        CheckMovement();
        HandleFootsteps();
    }

    /// <summary>
    /// Computes the player's world-space speed once per frame so we don't recompute
    /// (and accidentally double-multiply by walk/run speed) in two places.
    /// </summary>
    void ComputeCurrentSpeed()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        float magnitude = moveDirection.magnitude;
        currentSpeed = magnitude * (isRunning ? runSpeed : walkSpeed);
    }

    /// <summary>
    /// ���������, ��������� �� ����� �� �����.
    /// ���� ����� ������ ���������� ����� ��� ����� �������� ������ � ���������� PlayerMovement.
    /// ������������: ������� isGrounded ��������� ��������� � PlayerMovement.
    /// </summary>
    private bool IsPlayerGrounded()
    {
        PlayerMovement playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            return playerMovement.IsGrounded;
        }
        return false;
    }

    void CheckSurface()
    {
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance, groundLayerMask))
        {
            SurfaceType detectedSurface = DetectSurfaceType(hit.collider);

            if (detectedSurface != currentSurface && detectedSurface != SurfaceType.Unknown)
            {
                currentSurface = detectedSurface;
                UpdateSurfaceSettings();
            }
        }
    }

    SurfaceType DetectSurfaceType(Collider collider)
    {
        if (collider.CompareTag("Indoor"))
            return SurfaceType.Indoor;
        if (collider.CompareTag("Ground"))
            return SurfaceType.Ground;
        if (collider.CompareTag("Wet"))
            return SurfaceType.Wet;

        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (layerName == "Indoor")
            return SurfaceType.Indoor;
        if (layerName == "Ground")
            return SurfaceType.Ground;
        if (layerName == "Wet")
            return SurfaceType.Wet;

        return SurfaceType.Unknown;
    }

    void UpdateSurfaceSettings()
    {
        switch (currentSurface)
        {
            case SurfaceType.Indoor:
                currentClip = footstepsLitter;
                currentStepDuration = stepDurationLitter;
                currentTotalSteps = totalStepsLitter;
                break;

            case SurfaceType.Ground:
                currentClip = footstepsDirt;
                currentStepDuration = stepDurationDirt;
                currentTotalSteps = totalStepsDirt;
                break;

            case SurfaceType.Wet:
                currentClip = footstepsWater;
                // REVERTED to the original "Dirt" timings per user feedback —
                // the Water timings made steps sound worse in practice. The
                // Dirt values are a deliberate crutch; leave as is.
                currentStepDuration = stepDurationDirt;
                currentTotalSteps = totalStepsDirt;
                break;

            default:
                currentClip = footstepsLitter;
                currentStepDuration = stepDurationLitter;
                currentTotalSteps = totalStepsLitter;
                break;
        }

        audioSource.clip = currentClip;
    }

    void CheckMovement()
    {
        // currentSpeed is computed once in Update() so we don't accidentally
        // double-multiply it here and in HandleFootsteps.
        bool isMovingNow = currentSpeed > minSpeedForSteps && IsPlayerGrounded();

        if (isMovingNow != isMoving)
        {
            isMoving = isMovingNow;
            if (!isMoving)
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();
                isPlayingStep = false;
            }
        }
    }

    void HandleFootsteps()
    {
        if (!isMoving) return;

        // Hard-coded intervals per speed state — the user asked for this because
        // the inspector-tunable ranges (min/maxStepInterval, stepDuration*) were
        // confusing and the previous InverseLerp formula always clamped to the
        // fastest band due to a duplicate speed multiplier.
        float targetInterval = isRunning ? runningStepInterval : walkingStepInterval;

        // Don't fire steps faster than one clip length allows (avoids overlapping audio).
        float clipFloor = currentStepDuration * 0.5f;
        if (targetInterval < clipFloor) targetInterval = clipFloor;

        currentStepInterval = targetInterval;

        // Stop a step sound once its clip has finished.
        if (isPlayingStep && Time.time >= stepEndTime)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
            isPlayingStep = false;
        }

        // Play the next step if we're past the scheduled time.
        if (!isPlayingStep && Time.time >= nextStepTime)
        {
            PlayRandomStep();
            nextStepTime = Time.time + currentStepInterval;
        }
    }

    void PlayRandomStep()
    {
        if (currentClip == null) return;

        int stepIndex = Random.Range(0, currentTotalSteps);
        float startTime = stepIndex * currentStepDuration;

        if (startTime + currentStepDuration > currentClip.length)
        {
            startTime = Mathf.Max(0, currentClip.length - currentStepDuration);
        }

        audioSource.time = startTime;

        if (randomizePitch)
        {
            audioSource.pitch = 1f + Random.Range(-pitchRange, pitchRange);
        }

        audioSource.Play();
        isPlayingStep = true;
        stepEndTime = Time.time + currentStepDuration;
    }
}