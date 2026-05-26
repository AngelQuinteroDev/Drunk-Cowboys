using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using FPSMultiplayer.Core;
using FPSMultiplayer.Gameplay;

[RequireComponent(typeof(NetworkCharacterController))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(DrunkSystem))]
public class PlayerController : NetworkBehaviour
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
    [SerializeField] private float shootAnimDuration = 0.8f;

    [Header("Camera Noise")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private NoiseSettings drunkNoiseProfile;
    [SerializeField] private float maxNoiseAmplitude = 2.5f;
    [SerializeField] private float maxNoiseFrequency = 1.5f;

    private NetworkCharacterController _ncc;
    private CharacterController _cc;
    private HealthSystem _health;
    private DrunkSystem _drunk;
    private CinemachineBasicMultiChannelPerlin _noise;
    private PlayerInputHandler _inputHandler;

    private Vector3 _velocity;
    private float _verticalRotation;
    private float _predictedStamina;
    private float _shootAnimTimer;
    private bool _inputRegistered;
    private bool _isDead;

    [Networked] public Vector3 NetworkedVelocity { get; private set; }
    [Networked] public bool IsGrounded { get; private set; }
    [Networked] public bool IsSprinting { get; private set; }
    [Networked] public bool IsMoving { get; private set; }
    [Networked] public bool IsShooting { get; private set; }
    [Networked] public float LookPitch { get; private set; }
    [Networked] public float Stamina { get; private set; }

    [Networked] public int Kills { get; set; }
    [Networked] public int Deaths { get; set; }
    [Networked] public int RoundWins { get; set; }

    public override void Spawned()
    {
        _ncc = GetComponent<NetworkCharacterController>();
        _cc = GetComponent<CharacterController>();
        _health = GetComponent<HealthSystem>();
        _drunk = GetComponent<DrunkSystem>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _velocity = Vector3.zero;
        _verticalRotation = 0f;
        _shootAnimTimer = 0f;

        if (headPivot != null)
            headPivot.localRotation = Quaternion.identity;

        if (HasStateAuthority)
        {
            Stamina = maxStamina;
            LookPitch = 0f;
        }

        _predictedStamina = maxStamina;
        _isDead = false;

        if (HasInputAuthority)
        {
            SetupNoise();
            AttachLocalCamera();
            LockCursor(true);
            RegisterInputHandler();
        }

        if (_health != null)
        {
            _health.OnDeath.AddListener(OnDeath);
            _health.OnRespawn.AddListener(OnRespawn);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_inputRegistered && _inputHandler != null)
            runner.RemoveCallbacks(_inputHandler);
        
        if (_health != null)
        {
            _health.OnDeath.RemoveListener(OnDeath);
            _health.OnRespawn.RemoveListener(OnRespawn);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_isDead)
        {
            if (HasStateAuthority)
            {
                IsMoving = false;
                IsSprinting = false;
                IsShooting = false;
                NetworkedVelocity = Vector3.zero;
                _shootAnimTimer = 0f;
            }
            return;
        }

        if (_health != null && !_health.IsAlive)
            return;

        if (!GetInput(out PlayerInputData input))
            return;

        HandleLook(input.LookDelta);
        HandleMovement(input);
        HandleWeaponInput(input);

        if (HasStateAuthority)
            LookPitch = _verticalRotation;
    }

    public override void Render()
    {
        UpdateLookVisuals();
        UpdateAimTarget();
        UpdateDrunkNoise();
        UpdateAnimator();
    }

    private void HandleLook(Vector2 lookDelta)
    {
        if (headPivot == null) return;

        float mouseX = lookDelta.x * mouseSensitivity * Runner.DeltaTime;
        float mouseY = lookDelta.y * mouseSensitivity * Runner.DeltaTime;

        transform.Rotate(Vector3.up * mouseX);

        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, verticalLookMin, verticalLookMax);
    }

    private void HandleMovement(PlayerInputData input)
    {
        Vector3 moveDir = transform.TransformDirection(new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y));
        bool isMoving = moveDir.sqrMagnitude > 0.01f;
        bool wantSprint = input.Sprint && input.MoveDirection.y > 0.1f && isMoving;

        float stamina = HasStateAuthority ? Stamina : _predictedStamina;
        bool isSprinting = wantSprint && stamina > 0f;

        stamina += (isSprinting ? -staminaDrain : staminaRecovery) * Runner.DeltaTime;
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        if (HasStateAuthority)
        {
            Stamina = stamina;
            IsSprinting = isSprinting;
            IsMoving = isMoving;
        }
        else if (HasInputAuthority)
        {
            _predictedStamina = stamina;
        }

        float drunkPenalty = _drunk != null ? _drunk.GetMovementPenalty() : 1f;
        float speed = (isSprinting ? sprintSpeed : walkSpeed) * drunkPenalty;

        bool grounded = _cc != null ? _cc.isGrounded : _ncc.Grounded;
        if (grounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (input.Jump && grounded)
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        _velocity.y += gravity * Runner.DeltaTime;
        _velocity.x = moveDir.x * speed;
        _velocity.z = moveDir.z * speed;

        if (_cc != null)
            _cc.Move(_velocity * Runner.DeltaTime);
        else
            _ncc.Move(_velocity * Runner.DeltaTime);

        if (HasStateAuthority)
        {
            NetworkedVelocity = _velocity;
            IsGrounded = grounded;
        }
    }

    private void HandleWeaponInput(PlayerInputData input)
    {
        if (_health != null && !_health.IsAlive) return;
        if (weapon == null) return;

        bool roundActive = true;
        if (ServiceLocator.TryGet<NetworkRoundManager>(out var rm))
            roundActive = rm.IsRoundActive;

        bool fired = weapon.ProcessInput(
            input.Fire && roundActive,
            input.Reload,
            GetAimOrigin(),
            GetAimDirection(),
            Object.InputAuthority
        );

        if (HasStateAuthority)
        {
            if (fired)
            {
                IsShooting = true;
                _shootAnimTimer = shootAnimDuration;
            }
            else if (_shootAnimTimer > 0f)
            {
                _shootAnimTimer -= Runner.DeltaTime;
                if (_shootAnimTimer <= 0f)
                    IsShooting = false;
            }
        }
    }

    private void UpdateLookVisuals()
    {
        if (headPivot == null) return;
        float pitch = HasInputAuthority ? _verticalRotation : LookPitch;
        headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateAimTarget()
    {
        if (aimTarget == null) return;

        if (HasInputAuthority && Camera.main != null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            aimTarget.position = Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimMask)
                ? hit.point
                : ray.origin + ray.direction * aimDistance;
            return;
        }

        Vector3 origin = headPivot != null ? headPivot.position : transform.position;
        aimTarget.position = origin + GetAimDirection() * aimDistance;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isDrunk = _drunk != null && _drunk.GetDrunkRatio() >= drunkThreshold;
        bool isJumping = !IsGrounded;
        bool isIdle = !IsMoving && !IsShooting && !isJumping;

        animator.SetBool("isIdle", isIdle);
        animator.SetBool("IsWalking", IsMoving && !IsSprinting && !isJumping);
        animator.SetBool("IsRunning", IsSprinting && !isJumping);
        animator.SetBool("IsShooting", IsShooting);
        animator.SetBool("IsDrunk", isDrunk);
        animator.SetBool("IsDead", _health != null && !_health.IsAlive);
        animator.SetBool("IsJumping", isJumping);
    }

    private void UpdateDrunkNoise()
    {
        if (!HasInputAuthority || _noise == null || _drunk == null) return;

        float ratio = _drunk.GetDrunkRatio();
        _noise.AmplitudeGain = Mathf.Lerp(_noise.AmplitudeGain, ratio * maxNoiseAmplitude, Time.deltaTime * 2f);
        _noise.FrequencyGain = Mathf.Lerp(_noise.FrequencyGain, ratio * maxNoiseFrequency, Time.deltaTime * 2f);
    }

    private void OnDeath()
    {
        _isDead = true;

        if (animator != null) animator.SetBool("IsDead", true);
        if (weapon != null) weapon.enabled = false;

        if (HasInputAuthority)
        {
            LockCursor(false);
            if (_noise != null) { _noise.AmplitudeGain = 0f; _noise.FrequencyGain = 0f; }
        }
    }

    private void OnRespawn()
    {
        _isDead = false;
        _shootAnimTimer = 0f;

        if (weapon != null) weapon.enabled = true;
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool("IsDead", false);
            animator.SetBool("isIdle", true);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsShooting", false);
            animator.SetBool("IsDrunk", false);
            animator.SetBool("IsJumping", false);
        }
        if (HasInputAuthority) LockCursor(true);
    }

    public void ResetForRound(Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority) return;

        _velocity = Vector3.zero;
        _verticalRotation = 0f;
        _isDead = false;
        _shootAnimTimer = 0f;

        var uprightRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        _ncc.Teleport(position, uprightRotation);
        _health?.ForceRespawn();
        _drunk?.ResetDrunk();
        weapon?.ResetAmmo();
        Stamina = maxStamina;

        if (HasInputAuthority && headPivot != null)
            headPivot.localRotation = Quaternion.identity;
    }

    public float CurrentStamina =>
        HasStateAuthority ? Stamina : (HasInputAuthority ? _predictedStamina : Stamina);
    public float MaxStamina => maxStamina;

    private Vector3 GetAimOrigin()
    {
        if (HasInputAuthority && Camera.main != null)
            return Camera.main.transform.position;
        return headPivot != null ? headPivot.position : transform.position;
    }

    private Vector3 GetAimDirection()
    {
        if (HasInputAuthority && Camera.main != null)
            return Camera.main.transform.forward;
        Quaternion rot = Quaternion.Euler(LookPitch, transform.eulerAngles.y, 0f);
        return rot * Vector3.forward;
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

    private void RegisterInputHandler()
    {
        if (!HasInputAuthority) return;
        _inputHandler = GetComponent<PlayerInputHandler>();
        if (_inputHandler == null)
        {
            Debug.LogError("[PlayerController] Missing PlayerInputHandler on player prefab.");
            return;
        }
        Runner.AddCallbacks(_inputHandler);
        _inputRegistered = true;
    }

    private void AttachLocalCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Transform mount = headPivot != null ? headPivot : transform;
        cam.transform.SetParent(mount, false);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}