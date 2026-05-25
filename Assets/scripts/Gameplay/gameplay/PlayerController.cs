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

    [Header("Camera Noise")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private NoiseSettings drunkNoiseProfile;
    [SerializeField] private float maxNoiseAmplitude = 2.5f;
    [SerializeField] private float maxNoiseFrequency = 1.5f;

    private NetworkCharacterController _ncc;
    private HealthSystem _health;
    private DrunkSystem _drunk;

    private CinemachineBasicMultiChannelPerlin _noise;

    private Vector3 _velocity;

    private float _verticalRotation;
    private float _predictedStamina;

    private bool _inputRegistered;
    private PlayerInputHandler _inputHandler;

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
        _health = GetComponent<HealthSystem>();
        _drunk = GetComponent<DrunkSystem>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _verticalRotation = 0f;

        if (HasStateAuthority)
        {
            Stamina = maxStamina;
            LookPitch = 0f;
        }

        _predictedStamina = maxStamina;

        if (headPivot != null)
            headPivot.localRotation = Quaternion.identity;

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
    }

    private void SetupNoise()
    {
        if (virtualCamera == null)
            return;

        _noise = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (_noise == null)
            _noise = virtualCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();

        if (drunkNoiseProfile != null)
            _noise.NoiseProfile = drunkNoiseProfile;

        _noise.AmplitudeGain = 0f;
        _noise.FrequencyGain = 0f;
    }

    public override void FixedUpdateNetwork()
    {
        if (_health != null && !_health.IsAlive)
        {
            if (HasStateAuthority)
            {
                IsMoving = false;
                IsSprinting = false;
                IsShooting = false;
                NetworkedVelocity = Vector3.zero;
            }
            return;
        }

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
        if (headPivot == null)
            return;

        float mouseX = lookDelta.x * mouseSensitivity * Runner.DeltaTime;
        float mouseY = lookDelta.y * mouseSensitivity * Runner.DeltaTime;

        transform.Rotate(Vector3.up * mouseX);

        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, verticalLookMin, verticalLookMax);
    }

    private void HandleMovement(PlayerInputData input)
    {
        Vector3 moveDir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
        moveDir = transform.TransformDirection(moveDir);

        bool isMoving = moveDir.sqrMagnitude > 0.01f;
        bool wantsSprint = input.Sprint && input.MoveDirection.y > 0.1f && isMoving;

        float stamina = HasStateAuthority ? Stamina : _predictedStamina;
        bool canSprint = stamina > 0f;
        bool isSprinting = wantsSprint && canSprint;

        if (isSprinting)
            stamina -= staminaDrain * Runner.DeltaTime;
        else
            stamina += staminaRecovery * Runner.DeltaTime;

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

        if (_ncc.Grounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (input.Jump && _ncc.Grounded)
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        _velocity.y += gravity * Runner.DeltaTime;
        _velocity.x = moveDir.x * speed;
        _velocity.z = moveDir.z * speed;

        _ncc.Move(_velocity * Runner.DeltaTime);

        if (HasStateAuthority)
        {
            NetworkedVelocity = _velocity;
            IsGrounded = _ncc.Grounded;
        }
    }

    private void HandleWeaponInput(PlayerInputData input)
    {
        if (_health != null && !_health.IsAlive)
            return;

        if (weapon == null)
            return;

        if (HasStateAuthority)
        {
            bool canShoot = input.Fire && weapon.CanShoot;
            IsShooting = canShoot;
        }

        bool roundActive = true;
        if (ServiceLocator.TryGet<FPSMultiplayer.Gameplay.NetworkRoundManager>(out var roundManager))
            roundActive = roundManager.IsRoundActive;

        weapon.ProcessInput(
            input.Fire && roundActive,
            input.Reload,
            GetAimOrigin(),
            GetAimDirection(),
            Object.InputAuthority
        );
    }

    private void UpdateAimTarget()
    {
        if (aimTarget == null)
            return;

        if (HasInputAuthority && Camera.main != null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            aimTarget.position =
                Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimMask)
                ? hit.point
                : ray.origin + ray.direction * aimDistance;
            return;
        }

        Vector3 origin = headPivot != null ? headPivot.position : transform.position;
        Vector3 direction = GetAimDirection();
        aimTarget.position = origin + direction * aimDistance;
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        bool isDrunk = _drunk != null && _drunk.GetDrunkRatio() >= drunkThreshold;
        bool isIdle = !IsMoving && !IsShooting;
        bool isJumping = !IsGrounded;

        animator.SetBool("isIdle", isIdle);
        animator.SetBool("IsWalking", IsMoving && !IsSprinting);
        animator.SetBool("IsRunning", IsSprinting);
        animator.SetBool("IsShooting", IsShooting);
        animator.SetBool("IsDrunk", isDrunk);
        animator.SetBool("IsDead", _health != null && !_health.IsAlive);
        animator.SetBool("IsJumping", isJumping);
    }

    private void OnDeath()
    {
        if (animator != null)
            animator.SetBool("IsDead", true);

        if (weapon != null)
            weapon.enabled = false;

        enabled = false;

        if (HasInputAuthority)
            LockCursor(false);

        if (_noise != null)
        {
            _noise.AmplitudeGain = 0f;
            _noise.FrequencyGain = 0f;
        }
    }

    private void UpdateDrunkNoise()
    {
        if (!HasInputAuthority || _noise == null || _drunk == null)
            return;

        float ratio = _drunk.GetDrunkRatio();

        _noise.AmplitudeGain = Mathf.Lerp(
            _noise.AmplitudeGain,
            ratio * maxNoiseAmplitude,
            Time.deltaTime * 2f
        );

        _noise.FrequencyGain = Mathf.Lerp(
            _noise.FrequencyGain,
            ratio * maxNoiseFrequency,
            Time.deltaTime * 2f
        );
    }

    public float CurrentStamina =>
        HasStateAuthority ? Stamina : (HasInputAuthority ? _predictedStamina : Stamina);

    public float MaxStamina => maxStamina;

    private void UpdateLookVisuals()
    {
        if (headPivot == null)
            return;

        float pitch = HasInputAuthority ? _verticalRotation : LookPitch;
        headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

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

        float pitch = HasInputAuthority ? _verticalRotation : LookPitch;
        Quaternion rot = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
        return rot * Vector3.forward;
    }

    public void ResetForRound(Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority) return;

        _velocity = Vector3.zero;
        _ncc.Teleport(position, rotation);
        _health?.ForceRespawn();
        _drunk?.ResetDrunk();
        weapon?.ResetAmmo();
        Stamina = maxStamina;
    }

    private void OnRespawn()
    {
        if (weapon != null)
            weapon.enabled = true;

        if (HasInputAuthority)
            LockCursor(true);

        if (animator != null)
            animator.SetBool("IsDead", false);
    }

    private void RegisterInputHandler()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        if (_inputHandler == null)
        {
            Debug.LogError("[PlayerController] Missing PlayerInputHandler. Add it to the player prefab.");
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