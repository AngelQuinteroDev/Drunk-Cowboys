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
        Transform GetSpawnPoint(PlayerRef player);
        void SpawnExistingPlayers(NetworkRunner runner);
    }

    public class PlayerSpawner : MonoBehaviour, IPlayerSpawner
    {
        [SerializeField] private NetworkPrefabRef _playerPrefab;
        [SerializeField] private Transform[] _spawnPoints;

        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

        private void Awake()
        {
            ServiceLocator.Register<IPlayerSpawner>(this);
        }


        public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (_spawnedPlayers.ContainsKey(player)) return;

            if (_playerPrefab == NetworkPrefabRef.Empty)
            {
                Debug.LogError("[PlayerSpawner] Player prefab no asignado en el Inspector.");
                return;
            }

            var spawnPoint = GetSpawnPoint(player);
            Quaternion spawnRotation = GetUprightRotation(spawnPoint.rotation);

            var networkPlayer = runner.Spawn(
                _playerPrefab,
                spawnPoint.position,
                spawnRotation,
                player
            );

            if (networkPlayer == null)
            {
                Debug.LogError($"[PlayerSpawner] Spawn falló para player {player}. Verifica NetworkObject en el prefab y la Fusion Prefab Table.");
                return;
            }

            _spawnedPlayers[player] = networkPlayer;
            runner.SetPlayerObject(player, networkPlayer);
            EventBus.Publish(new PlayerSpawned { PlayerId = player.PlayerId });
            Debug.Log($"[PlayerSpawner] Player {player} spawneado correctamente.");
        }

        public void SpawnExistingPlayers(NetworkRunner runner)
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
            Debug.Log($"[PlayerSpawner] Player {player} despawneado.");
        }

        public Transform GetSpawnPoint(PlayerRef player)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
                return transform;

            int index = player.PlayerId % _spawnPoints.Length;
            return _spawnPoints[index];
        }

        private static Quaternion GetUprightRotation(Quaternion sourceRotation)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(sourceRotation * Vector3.forward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
                planarForward = Vector3.forward;

            return Quaternion.LookRotation(planarForward.normalized, Vector3.up);
        }
    }
}