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

        // ─── Estado replicado para animaciones ────────────────────────────────
        // Los clientes leen esto para animar correctamente sin sincronizar el Animator.
        [Networked] public Vector3 NetworkedVelocity  { get; private set; }
        [Networked] public bool    IsGrounded          { get; private set; }
        [Networked] public bool    IsRunning           { get; private set; }
        [Networked] public bool    IsCrouching         { get; private set; }
        [Networked] public float   LookAngle           { get; private set; } // pitch de cámara

        private NetworkCharacterController _ncc;
        private PlayerInputHandler         _inputHandler;
        private PlayerAnimatorController   _animator;
        private Vector3                    _velocity;
        private bool                       _inputRegistered;

        public override void Spawned()
        {
            _ncc          = GetComponent<NetworkCharacterController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
            _animator     = GetComponent<PlayerAnimatorController>();

            // Solo el jugador local necesita cámara
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

        // ─── FixedUpdateNetwork ───────────────────────────────────────────────
        // Corre en el tick de Fusion. En host: SimulationState Authority.
        // En cliente con InputAuthority: predicción local.
        public override void FixedUpdateNetwork()
        {
            // GetInput solo retorna datos si este objeto tiene InputAuthority en este tick
            if (!GetInput(out PlayerInputData input)) return;

            // Movimiento
            var move = new Vector3(input.MoveDirection.x, 0, input.MoveDirection.y);
            move = transform.TransformDirection(move);

            float speed = input.Sprint ? _sprintSpeed : _moveSpeed;
            _velocity.x = move.x * speed;
            _velocity.z = move.z * speed;

            // Gravedad
            if (_ncc.Grounded && _velocity.y < 0)
                _velocity.y = -2f;

            _velocity.y += _gravity * Runner.DeltaTime;

            // Salto
            if (input.Jump && _ncc.Grounded)
                _velocity.y = _jumpForce;

            _ncc.Move(_velocity * Runner.DeltaTime);

            // Rotación horizontal (yaw)
            transform.Rotate(Vector3.up, input.LookDelta.x * GameConstants.MouseSensitivity * Runner.DeltaTime);

            // Estado replicado — State Authority lo escribe, todos lo leen
            if (HasStateAuthority)
            {
                NetworkedVelocity = _velocity;
                IsGrounded        = _ncc.Grounded;
                IsRunning         = input.Sprint && move.magnitude > 0.1f;
                IsCrouching       = input.Crouch;
                LookAngle         = input.LookDelta.y;
            }
        }

        // Render(): interpola visualmente en clientes remotos, nunca lógica de juego aquí
        public override void Render()
        {
            _animator?.UpdateAnimatorState(NetworkedVelocity, IsGrounded, IsRunning, IsCrouching);
        }
    }
}