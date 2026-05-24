using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;

namespace FPSMultiplayer.Networking
{
    public interface ISessionManager
    {
        NetworkRunner Runner { get; }
        bool IsHost { get; }
        Task CreateRoom(string roomName, int maxPlayers = 8);
        Task JoinRoom(string roomName);
        Task Shutdown();
    }

    public class SessionManager : MonoBehaviour, ISessionManager, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _runnerPrefab;
        [SerializeField] private NetworkSceneManagerDefault _sceneManager;

        public NetworkRunner Runner { get; private set; }
        public bool IsHost => Runner != null && Runner.IsServer;

        public void Configure(NetworkRunner runnerPrefab, NetworkSceneManagerDefault sceneManager = null)
        {
            _runnerPrefab = runnerPrefab;
            if (sceneManager != null)
                _sceneManager = sceneManager;
        }

        public async Task CreateRoom(string roomName, int maxPlayers = 8)
        {
            if (!EnsureRunnerPrefab()) return;
            if (!TryGetActiveSceneRef(out var sceneRef)) return;

            Runner = Instantiate(_runnerPrefab);
            Runner.AddCallbacks(this);

            var sceneManager = ResolveSceneManager();

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode       = GameMode.Host,          
                SessionName    = roomName,
                PlayerCount    = maxPlayers,
                SceneManager   = sceneManager,
                Scene          = sceneRef,
            });

            if (!result.Ok)
                Debug.LogError($"[SessionManager] CreateRoom failed: {result.ShutdownReason}");
        }

        public async Task JoinRoom(string roomName)
        {
            if (!EnsureRunnerPrefab()) return;
            if (!TryGetActiveSceneRef(out var sceneRef)) return;

            Runner = Instantiate(_runnerPrefab);
            Runner.AddCallbacks(this);

            var sceneManager = ResolveSceneManager();

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode     = GameMode.Client,
                SessionName  = roomName,
                SceneManager = sceneManager,
                Scene        = sceneRef,
            });

            if (!result.Ok)
                Debug.LogError($"[SessionManager] JoinRoom failed: {result.ShutdownReason}");
        }

        public async Task Shutdown()
        {
            if (Runner != null)
                await Runner.Shutdown();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SessionManager] Player joined: {player}");
            EventBus.Publish(new PlayerJoinedEvent { PlayerId = player.PlayerId });

            if (runner.IsServer)
            {
                if (ServiceLocator.TryGet<IPlayerSpawner>(out var spawner))
                    spawner.SpawnPlayer(runner, player);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SessionManager] Player left: {player}");
            EventBus.Publish(new PlayerLeftEvent { PlayerId = player.PlayerId });

            if (runner.IsServer)
            {
                if (ServiceLocator.TryGet<IPlayerSpawner>(out var spawner))
                    spawner.DespawnPlayer(runner, player);
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            Debug.Log($"[SessionManager] Shutdown: {reason}");
            EventBus.Publish(new Core.Events.SceneChangeRequest { SceneName = Shared.GameConstants.Scene.MainMenu });
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        private bool EnsureRunnerPrefab()
        {
            if (_runnerPrefab != null) return true;

            Debug.LogError("[SessionManager] Runner Prefab not assigned. Assign it in Bootstrap or via inspector.");
            return false;
        }

        private NetworkSceneManagerDefault ResolveSceneManager()
        {
            if (_sceneManager != null) return _sceneManager;

            if (Runner != null)
            {
                _sceneManager = Runner.GetComponent<NetworkSceneManagerDefault>();
                if (_sceneManager == null)
                    _sceneManager = Runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            return _sceneManager;
        }

        private static bool TryGetActiveSceneRef(out SceneRef sceneRef)
        {
            sceneRef = default;
            var activeScene = SceneManager.GetActiveScene();

            if (!activeScene.IsValid())
            {
                Debug.LogError("[SessionManager] Active scene is invalid.");
                return false;
            }

            if (activeScene.buildIndex < 0 || activeScene.buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError("[SessionManager] Active scene is not in Build Settings.");
                return false;
            }

            sceneRef = SceneRef.FromIndex(activeScene.buildIndex);
            return true;
        }
    }
}