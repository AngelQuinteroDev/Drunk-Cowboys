// Corazón del networking. Crea/une salas. Expone el NetworkRunner.
// Implementa INetworkRunnerCallbacks para reaccionar a eventos de Fusion.
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
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

        // ─── Crear sala ───────────────────────────────────────────────────────
        public async Task CreateRoom(string roomName, int maxPlayers = 8)
        {
            Runner = Instantiate(_runnerPrefab);
            Runner.AddCallbacks(this);

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode       = GameMode.Host,           // Player Host topology
                SessionName    = roomName,
                PlayerCount    = maxPlayers,
                SceneManager   = _sceneManager,
                Scene          = SceneRef.None,           // Seguimos en la escena actual
            });

            if (!result.Ok)
                Debug.LogError($"[SessionManager] CreateRoom failed: {result.ShutdownReason}");
        }

        // ─── Unirse a sala ────────────────────────────────────────────────────
        public async Task JoinRoom(string roomName)
        {
            Runner = Instantiate(_runnerPrefab);
            Runner.AddCallbacks(this);

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode     = GameMode.Client,
                SessionName  = roomName,
                SceneManager = _sceneManager,
            });

            if (!result.Ok)
                Debug.LogError($"[SessionManager] JoinRoom failed: {result.ShutdownReason}");
        }

        public async Task Shutdown()
        {
            if (Runner != null)
                await Runner.Shutdown();
        }

        // ─── INetworkRunnerCallbacks ──────────────────────────────────────────
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SessionManager] Player joined: {player}");
            EventBus.Publish(new PlayerJoinedEvent { PlayerId = player.PlayerId });

            // Solo el host spawnea jugadores
            if (runner.IsServer)
            {
                var spawner = ServiceLocator.Get<IPlayerSpawner>();
                spawner?.SpawnPlayer(runner, player);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[SessionManager] Player left: {player}");
            EventBus.Publish(new PlayerLeftEvent { PlayerId = player.PlayerId });

            if (runner.IsServer)
            {
                var spawner = ServiceLocator.Get<IPlayerSpawner>();
                spawner?.DespawnPlayer(runner, player);
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            Debug.Log($"[SessionManager] Shutdown: {reason}");
            // Publicar evento para que UI y managers limpien su estado
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
    }
}