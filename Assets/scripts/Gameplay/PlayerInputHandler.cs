// Recolecta input local y lo entrega a Fusion via INetworkInput.
// Separa completamente el input del movimiento — patrón correcto para prediction.
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Sockets;
using FPSMultiplayer.Shared;

namespace FPSMultiplayer.Gameplay
{
    // Estructura de input — debe ser un NetworkInput struct
    public struct PlayerInputData : INetworkInput
    {
        public Vector2 MoveDirection;
        public Vector2 LookDelta;
        public NetworkBool Jump;
        public NetworkBool Sprint;
        public NetworkBool Crouch;
        public NetworkBool Fire;
        public NetworkBool AimDownSights;
        public NetworkBool Reload;
        public NetworkBool Interact;
    }

    // Este componente sólo vive en el objeto local del jugador.
    // OnInput() es el callback correcto — nunca recopilar input en FixedUpdateNetwork().
    public class PlayerInputHandler : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private InputActionAsset _inputAsset;

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        private InputAction _interactAction;
        private InputAction _attackAction;
        private InputAction _aimDownSightsAction;
        private InputAction _reloadAction;

        private Vector2 _lookDelta;
        private bool    _jumpPending;

        private void Awake()
        {
            if (_inputAsset == null)
            {
                Debug.LogError("[PlayerInputHandler] Missing InputActionAsset. Assign InputSystem_Actions in the inspector.");
                return;
            }

            _playerMap = _inputAsset.FindActionMap("Player", true);
            _moveAction = _playerMap.FindAction("Move", true);
            _lookAction = _playerMap.FindAction("Look", true);
            _jumpAction = _playerMap.FindAction("Jump", true);
            _sprintAction = _playerMap.FindAction("Sprint", true);
            _crouchAction = _playerMap.FindAction("Crouch", true);
            _interactAction = _playerMap.FindAction("Interact", true);

            _attackAction = _playerMap.FindAction("Attack", false);
            _aimDownSightsAction = _playerMap.FindAction("AimDownSights", false);
            _reloadAction = _playerMap.FindAction("Reload", false);
        }

        private void OnEnable()
        {
            _playerMap?.Enable();
            if (_jumpAction != null)
                _jumpAction.performed += OnJumpPerformed;
        }

        private void OnDisable()
        {
            if (_jumpAction != null)
                _jumpAction.performed -= OnJumpPerformed;
            _playerMap?.Disable();
        }

        private void OnJumpPerformed(InputAction.CallbackContext _)
        {
            _jumpPending = true;
        }

        // Fusion llama esto una vez por tick para recopilar el input del jugador local
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_playerMap == null) return;

            var data = new PlayerInputData
            {
                MoveDirection  = _moveAction.ReadValue<Vector2>(),
                LookDelta      = _lookDelta,
                Jump           = _jumpPending,
                Sprint         = _sprintAction.IsPressed(),
                Crouch         = _crouchAction.IsPressed(),
                Fire           = _attackAction != null && _attackAction.IsPressed(),
                AimDownSights  = _aimDownSightsAction != null && _aimDownSightsAction.IsPressed(),
                Reload         = _reloadAction != null && _reloadAction.IsPressed(),
                Interact       = _interactAction.IsPressed(),
            };

            _jumpPending = false;  // Consume el evento
            _lookDelta   = Vector2.zero;

            input.Set(data);
        }

        private void Update()
        {
            // El mouse delta se acumula aquí para no perderlo entre ticks de Fusion
            if (_lookAction != null)
                _lookDelta += _lookAction.ReadValue<Vector2>();
        }

        // Stubs requeridos por INetworkRunnerCallbacks
        public void OnPlayerJoined(NetworkRunner r, PlayerRef p) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef p) { }
        public void OnShutdown(NetworkRunner r, ShutdownReason reason) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress a, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
        public void OnSessionListUpdated(NetworkRunner r, System.Collections.Generic.List<SessionInfo> l) { }
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr m) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey k, System.ArraySegment<byte> d) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken t) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, System.Collections.Generic.Dictionary<string, object> d) { }
    }
}