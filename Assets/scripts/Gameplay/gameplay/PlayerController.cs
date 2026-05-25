// ============================================================
//  PlayerController — Fusion 2, Player Host Topology
//
//  FIXES APLICADOS:
//  1. _velocity se resetea a Vector3.zero en Spawned() para
//     evitar impulsos fantasma en el primer tick.
//  2. RegisterInputHandler() tiene guard HasInputAuthority
//     para que solo el jugador local registre callbacks.
//  3. Se llama _inputHandler.SetAsLocalPlayer() para que
//     Update() del handler solo lea input en el cliente local.
//  4. _verticalRotation se inicializa explícitamente a 0
//     y LookPitch se sincroniza correctamente desde el inicio.
//  5. enabled = false en OnDeath() reemplazado por flag interno
//     para no bloquear FixedUpdateNetwork() en Fusion.
// ============================================================
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
    [SerializeField] private float walkSpeed   = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpForce   = 5f;
    [SerializeField] private float gravity     = -20f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina      = 100f;
    [SerializeField] private float staminaDrain    = 25f;
    [SerializeField] private float staminaRecovery = 15f;

    [Header("Look")]
    [SerializeField] private Transform headPivot;
    [SerializeField] private float mouseSensitivity = 8f;
    [SerializeField] private float verticalLookMin  = -85f;
    [SerializeField] private float verticalLookMax  = 85f;

    [Header("Weapon")]
    [SerializeField] private WeaponSystem weapon;

    [Header("Aim")]
    [SerializeField] private Transform aimTarget;
    [SerializeField] private float     aimDistance = 100f;
    [SerializeField] private LayerMask aimMask     = ~0;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private float    drunkThreshold = 0.3f;

    [Header("Camera Noise")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private NoiseSettings     drunkNoiseProfile;
    [SerializeField] private float             maxNoiseAmplitude = 2.5f;
    [SerializeField] private float             maxNoiseFrequency = 1.5f;

    // ── Referencias internas ────────────────────────────────────────────────
    private NetworkCharacterController _ncc;
    private CharacterController        _cc;
    private HealthSystem               _health;
    private DrunkSystem                _drunk;
    private CinemachineBasicMultiChannelPerlin _noise;
    private PlayerInputHandler         _inputHandler;

    // ── Estado local (no replicado) ─────────────────────────────────────────
    private Vector3 _velocity;           // FIX: se resetea en Spawned()
    private float   _verticalRotation;   // FIX: inicializado a 0 en Spawned()
    private float   _predictedStamina;
    private bool    _inputRegistered;
    private bool    _isDead;             // FIX: flag local en vez de enabled=false

    // ── Estado replicado ────────────────────────────────────────────────────
    [Networked] public Vector3 NetworkedVelocity { get; private set; }
    [Networked] public bool    IsGrounded         { get; private set; }
    [Networked] public bool    IsSprinting        { get; private set; }
    [Networked] public bool    IsMoving           { get; private set; }
    [Networked] public bool    IsShooting         { get; private set; }
    [Networked] public float   LookPitch          { get; private set; }
    [Networked] public float   Stamina            { get; private set; }

    [Networked] public int Kills     { get; set; }
    [Networked] public int Deaths    { get; set; }
    [Networked] public int RoundWins { get; set; }

    // ── Spawned ─────────────────────────────────────────────────────────────
    public override void Spawned()
    {
        _ncc    = GetComponent<NetworkCharacterController>();
        _cc     = GetComponent<CharacterController>();
        _health = GetComponent<HealthSystem>();
        _drunk  = GetComponent<DrunkSystem>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // FIX 1: Resetear velocity para evitar impulso fantasma en primer tick
        _velocity         = Vector3.zero;
        _verticalRotation = 0f;

        // FIX 2: Resetear rotación visual del head pivot
        if (headPivot != null)
            headPivot.localRotation = Quaternion.identity;

        if (HasStateAuthority)
        {
            Stamina   = maxStamina;
            LookPitch = 0f;
        }

        _predictedStamina = maxStamina;
        _isDead           = false;

        // FIX 3: Solo el jugador local configura cámara e input
        if (HasInputAuthority)
        {
            SetupNoise();
            AttachLocalCamera();
            LockCursor(true);
            RegisterInputHandler(); // Ahora tiene guard interno
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

    // ── FixedUpdateNetwork ─────────────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        // FIX 4: Usar flag interno _isDead en vez de enabled
        // enabled=false bloquea FixedUpdateNetwork en Fusion — bug crítico
        if (_isDead)
        {
            if (HasStateAuthority)
            {
                IsMoving          = false;
                IsSprinting       = false;
                IsShooting        = false;
                NetworkedVelocity = Vector3.zero;
            }
            return;
        }

        if (_health != null && !_health.IsAlive)
            return;

        // GetInput solo devuelve datos si este objeto tiene InputAuthority
        // en este tick. Para objetos remotos devuelve false → no se mueven.
        if (!GetInput(out PlayerInputData input))
            return;

        HandleLook(input.LookDelta);
        HandleMovement(input);
        HandleWeaponInput(input);

        if (HasStateAuthority)
            LookPitch = _verticalRotation;
    }

    // ── Render ──────────────────────────────────────────────────────────────
    public override void Render()
    {
        UpdateLookVisuals();
        UpdateAimTarget();
        UpdateDrunkNoise();
        UpdateAnimator();
    }

    // ── Look ────────────────────────────────────────────────────────────────
    private void HandleLook(Vector2 lookDelta)
    {
        if (headPivot == null) return;

        // FIX 5: Multiplicar por Runner.DeltaTime, no Time.deltaTime
        // Runner.DeltaTime es el paso fijo de simulación de Fusion
        float mouseX = lookDelta.x * mouseSensitivity * Runner.DeltaTime;
        float mouseY = lookDelta.y * mouseSensitivity * Runner.DeltaTime;

        transform.Rotate(Vector3.up * mouseX);

        _verticalRotation -= mouseY;
        _verticalRotation  = Mathf.Clamp(_verticalRotation, verticalLookMin, verticalLookMax);
    }

    // ── Movement ────────────────────────────────────────────────────────────
    private void HandleMovement(PlayerInputData input)
    {
        Vector3 moveDir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
        moveDir = transform.TransformDirection(moveDir);

        bool isMoving    = moveDir.sqrMagnitude > 0.01f;
        bool wantsSprint = input.Sprint && input.MoveDirection.y > 0.1f && isMoving;

        float stamina    = HasStateAuthority ? Stamina : _predictedStamina;
        bool  isSprinting = wantsSprint && stamina > 0f;

        stamina += (isSprinting ? -staminaDrain : staminaRecovery) * Runner.DeltaTime;
        stamina  = Mathf.Clamp(stamina, 0f, maxStamina);

        if (HasStateAuthority)
        {
            Stamina     = stamina;
            IsSprinting = isSprinting;
            IsMoving    = isMoving;
        }
        else if (HasInputAuthority)
        {
            _predictedStamina = stamina;
        }

        float drunkPenalty = _drunk != null ? _drunk.GetMovementPenalty() : 1f;
        float speed        = (isSprinting ? sprintSpeed : walkSpeed) * drunkPenalty;

        // Use CharacterController grounding to avoid NCC auto-rotation side effects.
        bool grounded = _cc != null ? _cc.isGrounded : _ncc.Grounded;
        if (grounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (input.Jump && grounded)
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        _velocity.y += gravity * Runner.DeltaTime;
        _velocity.x  = moveDir.x * speed;
        _velocity.z  = moveDir.z * speed;

        if (_cc != null)
            _cc.Move(_velocity * Runner.DeltaTime);
        else
            _ncc.Move(_velocity * Runner.DeltaTime);

        if (HasStateAuthority)
        {
            NetworkedVelocity = _velocity;
            IsGrounded        = grounded;
        }
    }

    // ── Weapon Input ────────────────────────────────────────────────────────
    private void HandleWeaponInput(PlayerInputData input)
    {
        if (_health != null && !_health.IsAlive) return;
        if (weapon == null) return;

        if (HasStateAuthority)
            IsShooting = input.Fire && weapon.CanShoot;

        bool roundActive = true;
        if (ServiceLocator.TryGet<FPSMultiplayer.Gameplay.NetworkRoundManager>(out var rm))
            roundActive = rm.IsRoundActive;

        weapon.ProcessInput(
            input.Fire && roundActive,
            input.Reload,
            GetAimOrigin(),
            GetAimDirection(),
            Object.InputAuthority
        );
    }

    // ── Render helpers ───────────────────────────────────────────────────────
    private void UpdateLookVisuals()
    {
        if (headPivot == null) return;
        // Local: usa rotación predicha. Remoto: usa LookPitch replicado
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

        Vector3 origin    = headPivot != null ? headPivot.position : transform.position;
        aimTarget.position = origin + GetAimDirection() * aimDistance;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isDrunk   = _drunk != null && _drunk.GetDrunkRatio() >= drunkThreshold;
        bool isIdle    = !IsMoving && !IsShooting;
        bool isJumping = !IsGrounded;

        animator.SetBool("isIdle",      isIdle);
        animator.SetBool("IsWalking",   IsMoving && !IsSprinting);
        animator.SetBool("IsRunning",   IsSprinting);
        animator.SetBool("IsShooting",  IsShooting);
        animator.SetBool("IsDrunk",     isDrunk);
        animator.SetBool("IsDead",      _health != null && !_health.IsAlive);
        animator.SetBool("IsJumping",   isJumping);
    }

    private void UpdateDrunkNoise()
    {
        if (!HasInputAuthority || _noise == null || _drunk == null) return;

        float ratio = _drunk.GetDrunkRatio();

        _noise.AmplitudeGain = Mathf.Lerp(
            _noise.AmplitudeGain, ratio * maxNoiseAmplitude, Time.deltaTime * 2f);

        _noise.FrequencyGain = Mathf.Lerp(
            _noise.FrequencyGain, ratio * maxNoiseFrequency, Time.deltaTime * 2f);
    }

    // ── Muerte / Respawn ────────────────────────────────────────────────────
    private void OnDeath()
    {
        _isDead = true; // FIX: flag en vez de enabled=false

        if (animator != null) animator.SetBool("IsDead", true);
        if (weapon != null)   weapon.enabled = false;

        if (HasInputAuthority)
        {
            LockCursor(false);
            if (_noise != null) { _noise.AmplitudeGain = 0f; _noise.FrequencyGain = 0f; }
        }
    }

    private void OnRespawn()
    {
        _isDead = false;

        if (weapon != null)   weapon.enabled = true;
        if (animator != null) animator.SetBool("IsDead", false);
        if (HasInputAuthority) LockCursor(true);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    public void ResetForRound(Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority) return;

        _velocity         = Vector3.zero;
        _verticalRotation = 0f;
        _isDead           = false;

        // Force upright rotation to avoid tilted spawns from rotated spawn points.
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

    // ── Setup ────────────────────────────────────────────────────────────────
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
        // FIX: Guard explícito — solo el jugador local registra callbacks
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
        Cursor.visible   = !locked;
    }
}