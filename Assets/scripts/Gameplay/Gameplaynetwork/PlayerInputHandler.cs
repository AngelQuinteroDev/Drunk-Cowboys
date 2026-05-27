using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;

namespace FPSMultiplayer.Gameplay
{
    public struct PlayerInputData : INetworkInput
    {
        public Vector2      MoveDirection;
        public Vector2      LookDelta;
        public NetworkBool  Jump;
        public NetworkBool  Sprint;
        public NetworkBool  Crouch;
        public NetworkBool  Fire;
        public NetworkBool  AimDownSights;
        public NetworkBool  Reload;
        public NetworkBool  Interact;
    }

    public class PlayerInputHandler : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private InputActionAsset _inputAsset;
        [SerializeField] private float _moveDeadzone = 0.15f;
        [SerializeField] private float _lookDeadzone = 0.05f;

        private InputActionMap _playerMap;
        private InputAction    _moveAction;
        private InputAction    _lookAction;
        private InputAction    _jumpAction;
        private InputAction    _sprintAction;
        private InputAction    _crouchAction;
        private InputAction    _interactAction;
        private InputAction    _attackAction;
        private InputAction    _aimDownSightsAction;
        private InputAction    _reloadAction;

        private Vector2       _lookDelta;
        private bool          _jumpPending;
        private NetworkObject _netObject;

        private void Awake()
        {
            _netObject = GetComponent<NetworkObject>();

            if (_inputAsset == null)
            {
                Debug.LogError("[PlayerInputHandler] Asigna el InputActionAsset en el inspector.");
                return;
            }

            _playerMap           = _inputAsset.FindActionMap("Player", true);
            _moveAction          = _playerMap.FindAction("Move",          true);
            _lookAction          = _playerMap.FindAction("Look",          true);
            _jumpAction          = _playerMap.FindAction("Jump",          true);
            _sprintAction        = _playerMap.FindAction("Sprint",        true);
            _crouchAction        = _playerMap.FindAction("Crouch",        true);
            _interactAction      = _playerMap.FindAction("Interact",      true);
            _attackAction        = _playerMap.FindAction("Attack",        false);
            _aimDownSightsAction = _playerMap.FindAction("AimDownSights", false);
            _reloadAction        = _playerMap.FindAction("Reload",        false);
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
       
            if (IsLocalPlayer()) _jumpPending = true;
        }

        private void Update()
        {
            if (!IsLocalPlayer()) return;
            if (_lookAction != null)
                _lookDelta += _lookAction.ReadValue<Vector2>();
        }

      
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_playerMap == null) return;

            if (!IsLocalPlayer())
            {
                
                _jumpPending = false;
                _lookDelta   = Vector2.zero;
                input.Set(default(PlayerInputData));
                return;
            }

            var data = new PlayerInputData
            {
                MoveDirection  = ApplyDeadzone(_moveAction.ReadValue<Vector2>(), _moveDeadzone),
                LookDelta      = ApplyDeadzone(_lookDelta, _lookDeadzone),
                Jump           = _jumpPending,
                Sprint         = _sprintAction.IsPressed(),
                Crouch         = _crouchAction.IsPressed(),
                Fire           = _attackAction        != null && _attackAction.IsPressed(),
                AimDownSights  = _aimDownSightsAction != null && _aimDownSightsAction.IsPressed(),
                Reload         = _reloadAction        != null && _reloadAction.IsPressed(),
                Interact       = _interactAction.IsPressed(),
            };

            _jumpPending = false;
            _lookDelta   = Vector2.zero;

            input.Set(data);
        }

      
        private bool IsLocalPlayer()
        {
            if (_netObject == null)
                _netObject = GetComponent<NetworkObject>();
            return _netObject != null && _netObject.HasInputAuthority;
        }

        private static Vector2 ApplyDeadzone(Vector2 v, float dead)
            => v.sqrMagnitude < dead * dead ? Vector2.zero : v;

       
        public void OnPlayerJoined(NetworkRunner r, PlayerRef p) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef p) { }
        public void OnShutdown(NetworkRunner r, ShutdownReason reason) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress a, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
        public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> l) { }
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr m) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey k, System.ArraySegment<byte> d) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken t) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> d) { }
    }
}