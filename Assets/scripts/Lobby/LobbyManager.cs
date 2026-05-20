// Administra el estado del lobby. NetworkBehaviour porque vive en un NetworkObject.
// El host tiene StateAuthority y escribe. Los clientes leen y reaccionan.
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;

namespace FPSMultiplayer.Lobby
{
    public class LobbyManager : NetworkBehaviour
    {
        // Lista replicada de datos de lobby. Fusion replica IList<NetworkStruct> automáticamente.
        [Networked, Capacity(16)]
        public NetworkLinkedList<LobbyPlayerEntry> Players { get; }

        [Networked] public bool  MatchStarted  { get; private set; }
        [Networked] public float CountdownTime { get; private set; }

        private ChangeDetector _changes;

        public override void Spawned()
        {
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
            ServiceLocator.Register<LobbyManager>(this);

            EventBus.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Subscribe<PlayerLeftEvent>(OnPlayerLeft);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ServiceLocator.Unregister<LobbyManager>();
            EventBus.Unsubscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Unsubscribe<PlayerLeftEvent>(OnPlayerLeft);
        }

        private void OnPlayerJoined(PlayerJoinedEvent evt)
        {
            if (!HasStateAuthority) return;

            Players.Add(new LobbyPlayerEntry
            {
                PlayerId  = evt.PlayerId,
                IsReady   = false,
                IsHost    = evt.PlayerId == Runner.LocalPlayer.PlayerId,
                Name      = new NetworkString<_32>($"Player_{evt.PlayerId}")
            });
        }

        private void OnPlayerLeft(PlayerLeftEvent evt)
        {
            if (!HasStateAuthority) return;

            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId == evt.PlayerId)
                {
                    Players.Remove(Players[i]);
                    break;
                }
            }
        }

        // ─── Host Controls ────────────────────────────────────────────────────

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_StartMatch()
        {
            if (!HasStateAuthority) return;
            if (!AllPlayersReady()) return;

            MatchStarted = true;
            EventBus.Publish(new MatchStartRequested());

            // Trigger scene change
            ServiceLocator.Get<Infrastructure.ISceneFlowManager>()?.LoadGameplayScene();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_KickPlayer(int playerId)
        {
            if (!HasStateAuthority) return;

            foreach (var player in Runner.ActivePlayers)
            {
                if (player.PlayerId == playerId)
                {
                    Runner.Disconnect(player);
                    break;
                }
            }
        }

        public override void Render()
        {
            foreach (var change in _changes.DetectChanges(this, out _, out _))
            {
                if (change == nameof(Players))
                    EventBus.Publish(new LobbyListUpdated());
            }
        }

        private bool AllPlayersReady()
        {
            foreach (var p in Players)
                if (!p.IsReady && !p.IsHost) return false;
            return true;
        }
    }

    // Struct de datos por jugador en lobby. NetworkStruct para poder usarlo en listas replicadas.
    public struct LobbyPlayerEntry : INetworkStruct
    {
        public int                  PlayerId;
        public NetworkBool          IsReady;
        public NetworkBool          IsHost;
        public NetworkString<_32>   Name;
    }

    // Evento interno para actualizar la UI del lobby
    public readonly struct LobbyListUpdated { }

}