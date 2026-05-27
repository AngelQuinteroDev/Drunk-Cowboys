using UnityEngine;
using Fusion;
using FPSMultiplayer.Shared;

namespace FPSMultiplayer.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkCharacterController))]
    public class PlayerNetworkController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed   = 6f;
        [SerializeField] private float _sprintSpeed = 10f;
        [SerializeField] private float _jumpForce   = 5f;
        [SerializeField] private float _gravity     = -20f;

        [Header("References")]
        [SerializeField] private Transform _cameraMount;

        // Pitch acumulado solo en StateAuthority para sincronizar la cámara
        [Networked] private float _networkPitch { get; set; }

        [Networked] public Vector3 NetworkedVelocity  { get; private set; }
        [Networked] public bool    IsGrounded          { get; private set; }
        [Networked] public bool    IsRunning           { get; private set; }
        [Networked] public bool    IsCrouching         { get; private set; }
        [Networked] public float   LookAngle           { get; private set; }

        private NetworkCharacterController _ncc;
        private PlayerInputHandler         _inputHandler;
        private PlayerAnimatorController   _animator;
        private WeaponSystem               _weapon;
        private Vector3                    _velocity;
        private bool                       _inputRegistered;

        // Pitch local (solo para el cliente con InputAuthority)
        private float _localPitch;

        public override void Spawned()
        {
            _ncc          = GetComponent<NetworkCharacterController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
            _animator     = GetComponent<PlayerAnimatorController>();
            _weapon       = GetComponentInChildren<WeaponSystem>();

            if (HasInputAuthority)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.SetParent(_cameraMount, false);
                    cam.transform.localPosition = Vector3.zero;
                    cam.transform.localRotation = Quaternion.identity;
                }

                if (_inputHandler != null)
                {
                    Runner.AddCallbacks(_inputHandler);
                    _inputRegistered = true;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_inputRegistered && _inputHandler != null)
            {
                runner.RemoveCallbacks(_inputHandler);
                _inputRegistered = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out PlayerInputData input)) return;

            // ── Movimiento ────────────────────────────────────────────────
            var move = new Vector3(input.MoveDirection.x, 0, input.MoveDirection.y);
            move = transform.TransformDirection(move);

            float speed = input.Sprint ? _sprintSpeed : _moveSpeed;
            _velocity.x = move.x * speed;
            _velocity.z = move.z * speed;

            if (_ncc.Grounded && _velocity.y < 0)
                _velocity.y = -2f;

            _velocity.y += _gravity * Runner.DeltaTime;

            if (input.Jump && _ncc.Grounded)
                _velocity.y = _jumpForce;

            _ncc.Move(_velocity * Runner.DeltaTime);

            // ── Rotación Yaw (horizontal — todo el cuerpo gira) ───────────
            transform.Rotate(Vector3.up, input.LookDelta.x * GameConstants.MouseSensitivity * Runner.DeltaTime);

            // ── Pitch (vertical — solo la cámara/muzzle) ─────────────────
            // Acumular pitch en StateAuthority para que el servidor
            // calcule la dirección correcta del disparo
            if (HasStateAuthority)
            {
                _networkPitch = Mathf.Clamp(
                    _networkPitch - input.LookDelta.y * GameConstants.MouseSensitivity * Runner.DeltaTime,
                    -80f, 80f
                );

                if (_cameraMount != null)
                    _cameraMount.localRotation = Quaternion.Euler(_networkPitch, 0f, 0f);
            }

            // ── Disparo: dirección desde cameraMount del servidor ─────────
            if (_weapon != null && HasStateAuthority)
            {
                // Obtener muzzle position y la dirección de apuntado real
                Vector3 shootOrigin = _cameraMount != null
                    ? _cameraMount.position
                    : transform.position + Vector3.up * 1.6f;

                Vector3 shootDir = _cameraMount != null
                    ? _cameraMount.forward
                    : transform.forward;

                _weapon.ProcessInput(
                    fire:      input.Fire,
                    reload:    input.Reload,
                    origin:    shootOrigin,
                    direction: shootDir,
                    owner:     Object.InputAuthority
                );
            }

            // ── Estado sincronizado ───────────────────────────────────────
            if (HasStateAuthority)
            {
                NetworkedVelocity = _velocity;
                IsGrounded        = _ncc.Grounded;
                IsRunning         = input.Sprint && move.magnitude > 0.1f;
                IsCrouching       = input.Crouch;
                LookAngle         = input.LookDelta.y;
            }
        }

        public override void Render()
        {
            _animator?.UpdateAnimatorState(NetworkedVelocity, IsGrounded, IsRunning, IsCrouching);

            // Aplicar pitch en el cliente local para respuesta inmediata
            if (HasInputAuthority && _cameraMount != null)
            {
                _localPitch = Mathf.Clamp(
                    _localPitch - Input.GetAxisRaw("Mouse Y") * GameConstants.MouseSensitivity * Time.deltaTime,
                    -80f, 80f
                );
                _cameraMount.localRotation = Quaternion.Euler(_localPitch, 0f, 0f);
            }
        }
    }
}