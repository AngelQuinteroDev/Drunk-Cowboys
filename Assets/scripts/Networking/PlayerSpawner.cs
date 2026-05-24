using System.Collections.Generic;
using UnityEngine;
using Fusion;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;

namespace FPSMultiplayer.Networking
{
    public interface IPlayerSpawner
    {
        void SpawnPlayer(NetworkRunner runner, PlayerRef player);
        void DespawnPlayer(NetworkRunner runner, PlayerRef player);
    }

    public class PlayerSpawner : MonoBehaviour, IPlayerSpawner
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private Transform[]   _spawnPoints;

        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

        private void Start()
        {
            ServiceLocator.Register<IPlayerSpawner>(this);

            if (ServiceLocator.TryGet<ISessionManager>(out var sessionManager))
            {
                var runner = sessionManager.Runner;
                if (runner != null && runner.IsServer)
                    SpawnExistingPlayers(runner);
            }
        }

        public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (_spawnedPlayers.ContainsKey(player)) return;

            var spawnPoint = GetSpawnPoint(player);
            var networkPlayer = runner.Spawn(
                _playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                player 
            );

            _spawnedPlayers[player] = networkPlayer;
            EventBus.Publish(new PlayerSpawned { PlayerId = player.PlayerId });
        }

        private void SpawnExistingPlayers(NetworkRunner runner)
        {
            foreach (var player in runner.ActivePlayers)
            {
                if (_spawnedPlayers.ContainsKey(player)) continue;
                SpawnPlayer(runner, player);
            }
        }

        public void DespawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (!_spawnedPlayers.TryGetValue(player, out var networkObject)) return;

            runner.Despawn(networkObject);
            _spawnedPlayers.Remove(player);
        }

        private Transform GetSpawnPoint(PlayerRef player)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
                return transform;

            int index = player.PlayerId % _spawnPoints.Length;
            return _spawnPoints[index];
        }
    }
}