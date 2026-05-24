using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(DrunkSystem))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrain = 25f;
    [SerializeField] private float staminaRecovery = 15f;

    [Header("Look")]
    [SerializeField] private Transform headPivot;
    [SerializeField] private float mouseSensitivity = 8f;
    [SerializeField] private float verticalLookMin = -85f;
    [SerializeField] private float verticalLookMax = 85f;

    [Header("Weapon")]
    [SerializeField] private WeaponSystem weapon;

    [Header("Aim")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private LayerMask aimMask = ~0;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private float drunkThreshold = 0.3f;

    [Header("Camera Noise")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private NoiseSettings drunkNoiseProfile;
    [SerializeField] private float maxNoiseAmplitude = 2.5f;
    [SerializeField] private float maxNoiseFrequency = 1.5f;

    private CharacterController _cc;
    private HealthSystem _health;
    private DrunkSystem _drunk;

    private CinemachineBasicMultiChannelPerlin _noise;

    private Vector3 _velocity;

    private float _verticalRotation;

    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isMoving;
    private bool _isShooting;

    private float _currentStamina;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        _health = GetComponent<HealthSystem>();

        _drunk = GetComponent<DrunkSystem>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        SetupNoise();

        _currentStamina = maxStamina;

        Cursor.lockState = CursorLockMode.Locked;

        Cursor.visible = false;

        if (_health != null)
            _health.OnDeath.AddListener(OnDeath);
    }

    private void SetupNoise()
    {
        if (virtualCamera == null)
            return;

        _noise =
            virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (_noise == null)
        {
            _noise =
                virtualCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
        }

        if (drunkNoiseProfile != null)
            _noise.NoiseProfile = drunkNoiseProfile;

        _noise.AmplitudeGain = 0f;
        _noise.FrequencyGain = 0f;
    }

    private void Update()
    {
        if (!_health.IsAlive)
            return;

        HandleLook();

        HandleMovement();

        HandleWeaponInput();

        UpdateAimTarget();

        UpdateDrunkNoise();

        UpdateAnimator();
    }

    private void HandleLook()
    {
        if (Mouse.current == null || headPivot == null)
            return;

        Vector2 delta =
            Mouse.current.delta.ReadValue();

        float mouseX =
            delta.x * mouseSensitivity * Time.deltaTime;

        float mouseY =
            delta.y * mouseSensitivity * Time.deltaTime;

        transform.Rotate(Vector3.up * mouseX);

        _verticalRotation -= mouseY;

        _verticalRotation = Mathf.Clamp(
            _verticalRotation,
            verticalLookMin,
            verticalLookMax
        );

        headPivot.localRotation =
            Quaternion.Euler(
                _verticalRotation,
                0f,
                0f
            );
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
            return;

        _isGrounded = _cc.isGrounded;

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        float h =
            (Keyboard.current.dKey.isPressed ? 1f : 0f)
            - (Keyboard.current.aKey.isPressed ? 1f : 0f);

        float v =
            (Keyboard.current.wKey.isPressed ? 1f : 0f)
            - (Keyboard.current.sKey.isPressed ? 1f : 0f);

        Vector3 moveDir =
            (transform.right * h +
             transform.forward * v).normalized;

        _isMoving = moveDir.magnitude > 0.1f;

        bool wantsSprint =
            Keyboard.current.leftShiftKey.isPressed &&
            v > 0f &&
            _isMoving &&
            _currentStamina > 0f;

        _isSprinting = wantsSprint;

        if (_isSprinting)
        {
            _currentStamina -=
                staminaDrain * Time.deltaTime;

            if (_currentStamina <= 0f)
            {
                _currentStamina = 0f;
                _isSprinting = false;
            }
        }
        else
        {
            _currentStamina +=
                staminaRecovery * Time.deltaTime;

            _currentStamina =
                Mathf.Clamp(
                    _currentStamina,
                    0f,
                    maxStamina
                );
        }

        float drunkPenalty =
            _drunk != null
            ? _drunk.GetMovementPenalty()
            : 1f;

        float speed =
            (_isSprinting ? sprintSpeed : walkSpeed)
            * drunkPenalty;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            TryJump();

        _velocity.y += gravity * Time.deltaTime;

        _cc.Move(
            (
                moveDir * speed +
                new Vector3(0f, _velocity.y, 0f)
            ) * Time.deltaTime
        );
    }

    private void HandleWeaponInput()
    {
        if (!_health.IsAlive)
            return;

        if (weapon == null || Mouse.current == null)
            return;

        bool shootPressed =
            Mouse.current.leftButton.isPressed;

        _isShooting =
            shootPressed &&
            !weapon.IsReloading &&
            weapon.CurrentAmmo > 0;

        if (_isShooting)
            weapon.TryShoot();

        if (Keyboard.current.rKey.wasPressedThisFrame)
            weapon.TryReload();
    }

    private void UpdateAimTarget()
    {
        if (aimTarget == null || Camera.main == null)
            return;

        Ray ray =
            Camera.main.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

        aimTarget.position =
            Physics.Raycast(
                ray,
                out RaycastHit hit,
                aimDistance,
                aimMask
            )
            ? hit.point
            : ray.origin + ray.direction * aimDistance;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        bool isDrunk =
            _drunk != null &&
            _drunk.GetDrunkRatio() >= drunkThreshold;

        bool isIdle =
            !_isMoving &&
            !_isShooting;

        bool isJumping =
            !_isGrounded;

        animator.SetBool("isIdle", isIdle);

        animator.SetBool(
            "IsWalking",
            _isMoving && !_isSprinting
        );

        animator.SetBool(
            "IsRunning",
            _isSprinting
        );

        animator.SetBool(
            "IsShooting",
            _isShooting
        );

        animator.SetBool(
            "IsDrunk",
            isDrunk
        );

        animator.SetBool(
            "IsDead",
            false
        );

        animator.SetBool(
            "IsJumping",
            isJumping
        );
    }

    private void OnDeath()
    {
        if (animator != null)
            animator.SetBool("IsDead", true);

        if (weapon != null)
            weapon.enabled = false;

        enabled = false;

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        if (_noise != null)
        {
            _noise.AmplitudeGain = 0f;
            _noise.FrequencyGain = 0f;
        }
    }

    private void TryJump()
    {
        if (!_isGrounded)
            return;

        _velocity.y =
            Mathf.Sqrt(
                jumpForce * -2f * gravity
            );
    }

    private void UpdateDrunkNoise()
    {
        if (_noise == null || _drunk == null)
            return;

        float ratio =
            _drunk.GetDrunkRatio();

        _noise.AmplitudeGain =
            Mathf.Lerp(
                _noise.AmplitudeGain,
                ratio * maxNoiseAmplitude,
                Time.deltaTime * 2f
            );

        _noise.FrequencyGain =
            Mathf.Lerp(
                _noise.FrequencyGain,
                ratio * maxNoiseFrequency,
                Time.deltaTime * 2f
            );
    }

    public float CurrentStamina =>
        _currentStamina;

    public float MaxStamina =>
        maxStamina;

    public bool IsGrounded =>
        _isGrounded;

    public bool IsSprinting =>
        _isSprinting;
}