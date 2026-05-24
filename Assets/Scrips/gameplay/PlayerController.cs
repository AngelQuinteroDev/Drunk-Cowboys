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
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private Transform headPivot;
    [SerializeField] private float mouseSensitivity = 8f;
    [SerializeField] private float verticalLookMin = -85f;
    [SerializeField] private float verticalLookMax = 85f;

    [Header("Weapon")]
    [SerializeField] private WeaponSystem weapon;

    [Header("Aim Target")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private LayerMask aimMask = ~0;

    [Header("Crouch")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Cover")]
    [SerializeField] private float coverCheckDistance = 1.2f;
    [SerializeField] private LayerMask coverLayer;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private float drunkThreshold = 0.3f;

    [Header("Cinemachine Drunk Noise")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private NoiseSettings drunkNoiseProfile;
    [SerializeField] private float maxNoiseAmplitude = 2.5f;
    [SerializeField] private float maxNoiseFrequency = 1.5f;

    private static readonly int _hashIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int _hashIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int _hashIsShooting = Animator.StringToHash("IsShooting");
    private static readonly int _hashIsDead = Animator.StringToHash("IsDead");
    private static readonly int _hashIsDrunk = Animator.StringToHash("IsDrunk");
    private static readonly int _hashIsDrinking = Animator.StringToHash("IsDrinking");
    private static readonly int _hashIsIdel = Animator.StringToHash("IsIdel");

    private CharacterController _cc;
    private HealthSystem _health;
    private DrunkSystem _drunk;

    private CinemachineBasicMultiChannelPerlin _noise;

    private Vector3 _velocity;
    private float _verticalRotation;
    private float _targetCCHeight;
    private Vector3 _targetCCCenter;

    private bool _isCrouching;
    private bool _isInCover;
    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isMoving;
    private bool _isShooting;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _health = GetComponent<HealthSystem>();
        _drunk = GetComponent<DrunkSystem>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _targetCCHeight = standHeight;
        _targetCCCenter = new Vector3(0f, standHeight / 2f, 0f);

        SetupNoise();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_health != null)
            _health.OnDeath.AddListener(OnDeath);
    }

    private void SetupNoise()
    {
        if (virtualCamera == null) return;

        _noise = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (_noise == null)
            _noise = virtualCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();

        if (drunkNoiseProfile != null)
            _noise.NoiseProfile = drunkNoiseProfile;

        _noise.AmplitudeGain = 0f;
        _noise.FrequencyGain = 0f;
    }

    private void Update()
    {
        if (!_health.IsAlive) return;

        HandleLook();
        HandleMovement();
        HandleCrouch();
        HandleWeaponInput();
        UpdateAimTarget();
        UpdateDrunkNoise();
        UpdateAnimator();
    }

    private void HandleLook()
    {
        if (Mouse.current == null || headPivot == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        float mouseX = delta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = delta.y * mouseSensitivity * Time.deltaTime;

        transform.Rotate(Vector3.up * mouseX);

        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, verticalLookMin, verticalLookMax);

        headPivot.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null) return;

        _isGrounded = _cc.isGrounded;
        if (_isGrounded && _velocity.y < 0f) _velocity.y = -2f;

        float h = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
        float v = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);

        Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;
        _isMoving = moveDir.magnitude > 0.1f;

        float drunkPenalty = _drunk != null ? _drunk.GetMovementPenalty() : 1f;
        _isSprinting = Keyboard.current.leftShiftKey.isPressed && !_isCrouching && v > 0f && _isMoving;
        float baseSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
        float speed = baseSpeed * drunkPenalty;

        if (Keyboard.current.spaceKey.wasPressedThisFrame) TryJump();
        if (Keyboard.current.cKey.wasPressedThisFrame) ToggleCrouch();
        if (Keyboard.current.qKey.wasPressedThisFrame) ToggleCover();

        _velocity.y += gravity * Time.deltaTime;
        _cc.Move((moveDir * speed + new Vector3(0f, _velocity.y, 0f)) * Time.deltaTime);
    }

    private void HandleWeaponInput()
    {
        if (weapon == null || Mouse.current == null || Keyboard.current == null) return;

        bool shootPressed = Mouse.current.leftButton.isPressed;
        _isShooting = shootPressed && !weapon.IsReloading && weapon.CurrentAmmo > 0;

        if (shootPressed)
            weapon.TryShoot();

        if (Keyboard.current.rKey.wasPressedThisFrame)
            weapon.TryReload();
    }

    private void UpdateAimTarget()
    {
        if (aimTarget == null || Camera.main == null) return;

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimMask))
            aimTarget.position = hit.point;
        else
            aimTarget.position = ray.origin + ray.direction * aimDistance;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isDrunk = _drunk != null && _drunk.GetDrunkRatio() >= drunkThreshold;
        bool isIdle = !_isMoving && !_isShooting;

        animator.SetBool(_hashIsIdel, isIdle);
        animator.SetBool(_hashIsWalking, _isMoving && !_isSprinting);
        animator.SetBool(_hashIsRunning, _isSprinting);
        animator.SetBool(_hashIsShooting, _isShooting);
        animator.SetBool(_hashIsDrunk, isDrunk);
        animator.SetBool(_hashIsDead, false);
        animator.SetBool(_hashIsDrinking, false);
    }

    public void TriggerDrinking()
    {
        if (animator == null) return;
        StartCoroutine(DrinkingRoutine());
    }

    private System.Collections.IEnumerator DrinkingRoutine()
    {
        animator.SetBool(_hashIsDrinking, true);
        yield return new WaitForSeconds(1.5f);
        animator.SetBool(_hashIsDrinking, false);
    }

    private void OnDeath()
    {
        if (animator != null)
            animator.SetBool(_hashIsDead, true);
    }

    private void TryJump()
    {
        if (!_isGrounded) return;
        if (_isCrouching) { ToggleCrouch(); return; }
        _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
    }

    private void ToggleCrouch()
    {
        if (_isInCover) return;
        _isCrouching = !_isCrouching;

        if (_isCrouching)
        {
            _targetCCHeight = crouchHeight;
            _targetCCCenter = new Vector3(0f, crouchHeight / 2f, 0f);
        }
        else
        {
            if (!CanStandUp()) { _isCrouching = true; return; }
            _targetCCHeight = standHeight;
            _targetCCCenter = new Vector3(0f, standHeight / 2f, 0f);
        }
    }

    private void HandleCrouch()
    {
        _cc.height = Mathf.Lerp(_cc.height, _targetCCHeight, Time.deltaTime * crouchTransitionSpeed);
        _cc.center = Vector3.Lerp(_cc.center, _targetCCCenter, Time.deltaTime * crouchTransitionSpeed);

        if (headPivot == null) return;
        float targetHeadY = _isCrouching ? crouchHeight - 0.15f : standHeight - 0.15f;
        Vector3 lp = headPivot.localPosition;
        headPivot.localPosition = new Vector3(lp.x, Mathf.Lerp(lp.y, targetHeadY, Time.deltaTime * crouchTransitionSpeed), lp.z);
    }

    private bool CanStandUp()
    {
        Vector3 origin = transform.position + Vector3.up * crouchHeight;
        return !Physics.SphereCast(origin, _cc.radius, Vector3.up, out _, standHeight - crouchHeight + 0.1f);
    }

    private void ToggleCover()
    {
        if (_isInCover) { ExitCover(); return; }
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, transform.forward, coverCheckDistance, coverLayer))
            EnterCover();
    }

    private void EnterCover()
    {
        _isInCover = _isCrouching = true;
        _targetCCHeight = crouchHeight;
        _targetCCCenter = new Vector3(0f, crouchHeight / 2f, 0f);
    }

    private void ExitCover()
    {
        _isInCover = _isCrouching = false;
        _targetCCHeight = standHeight;
        _targetCCCenter = new Vector3(0f, standHeight / 2f, 0f);
    }

    private void UpdateDrunkNoise()
    {
        if (_noise == null || _drunk == null) return;
        float ratio = _drunk.GetDrunkRatio();
        _noise.AmplitudeGain = Mathf.Lerp(_noise.AmplitudeGain, ratio * maxNoiseAmplitude, Time.deltaTime * 2f);
        _noise.FrequencyGain = Mathf.Lerp(_noise.FrequencyGain, ratio * maxNoiseFrequency, Time.deltaTime * 2f);
    }

    public bool IsCrouching => _isCrouching;
    public bool IsInCover => _isInCover;
    public bool IsGrounded => _isGrounded;
    public bool IsSprinting => _isSprinting;
}