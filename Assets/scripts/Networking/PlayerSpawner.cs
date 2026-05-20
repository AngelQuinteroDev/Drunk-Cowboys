// El host spawnea NetworkObjects para cada jugador.
// Mapea PlayerRef → NetworkObject para despachar correctamente.
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
        }

        public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            // Solo ejecuta en el host (State Authority)
            if (!runner.IsServer) return;

            var spawnPoint = GetSpawnPoint(player);
            var networkPlayer = runner.Spawn(
                _playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                player  // Asigna InputAuthority al jugador que se conecta
            );

            _spawnedPlayers[player] = networkPlayer;
            EventBus.Publish(new PlayerSpawned { PlayerId = player.PlayerId });
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

            // Distribución simple por índice; reemplaza con lógica de equipos cuando sea necesario
            int index = player.PlayerId % _spawnPoints.Length;
            return _spawnPoints[index];
        }
    }
}