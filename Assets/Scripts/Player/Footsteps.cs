using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class Footsteps : MonoBehaviour
{
    [Header("���� � ���������")]
    public AudioClip footstepsIndoor;
    public float stepDurationIndoor = 0.4f;
    public int totalStepsIndoor = 20;

    [Header("���� �� �����")]
    public AudioClip footstepsGround;
    public float stepDurationGround = 0.4f;
    public int totalStepsGround = 20;

    [Header("��������� ��������")]
    public float minStepInterval = 0.3f;
    public float maxStepInterval = 0.8f;
    public float minSpeedForSteps = 0.1f;
    public float walkSpeed = 5f;
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

        inputActions.Player.Sprint.performed += ctx => isRunning = true;
        inputActions.Player.Sprint.canceled += ctx => isRunning = false;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled -= ctx => moveInput = Vector2.zero;
        inputActions.Player.Sprint.performed -= ctx => isRunning = true;
        inputActions.Player.Sprint.canceled -= ctx => isRunning = false;

        inputActions.Player.Disable();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
        isPlayingStep = false;
    }

    void Update()
    {
        CheckSurface();
        CheckMovement();
        HandleFootsteps();
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

        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (layerName == "Indoor")
            return SurfaceType.Indoor;
        if (layerName == "Ground")
            return SurfaceType.Ground;

        return SurfaceType.Unknown;
    }

    void UpdateSurfaceSettings()
    {
        switch (currentSurface)
        {
            case SurfaceType.Indoor:
                currentClip = footstepsIndoor;
                currentStepDuration = stepDurationIndoor;
                currentTotalSteps = totalStepsIndoor;
                break;

            case SurfaceType.Ground:
                currentClip = footstepsGround;
                currentStepDuration = stepDurationGround;
                currentTotalSteps = totalStepsGround;
                break;

            default:
                currentClip = footstepsIndoor;
                currentStepDuration = stepDurationIndoor;
                currentTotalSteps = totalStepsIndoor;
                break;
        }

        audioSource.clip = currentClip;
    }

    void CheckMovement()
    {
        // �������� ����������� �������� �� Input System
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        float currentSpeed = moveDirection.magnitude;

        // ������������ ������� �������� � ������ ����
        if (isRunning)
            currentSpeed *= runSpeed;
        else
            currentSpeed *= walkSpeed;

        // ���������, ��� ����� �� ����� � �������� ���� ������
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

        // �������� ������� ��������
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        float currentSpeed = moveDirection.magnitude;

        // ������������ �������� � ������ ����
        if (isRunning)
            currentSpeed *= runSpeed;
        else
            currentSpeed *= walkSpeed;

        // ��������� �������� ����� �� ������ ��������
        float speedRatio = Mathf.InverseLerp(minSpeedForSteps, runSpeed, currentSpeed);
        currentStepInterval = Mathf.Lerp(maxStepInterval, minStepInterval, speedRatio);

        // ���������� ���������������� ����
        if (isPlayingStep && Time.time >= stepEndTime)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
            isPlayingStep = false;
        }

        // ��������������� ���������� ����
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