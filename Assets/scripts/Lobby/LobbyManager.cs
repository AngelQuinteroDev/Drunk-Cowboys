using System.Collections.Generic;
using Fusion;
using UnityEngine;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;

namespace FPSMultiplayer.Lobby
{
    public class LobbyManager : NetworkBehaviour
    {
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

            if (HasStateAuthority)
                ResetLobbyState();

            EventBus.Publish(new LobbyListUpdated());
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

            EnsurePlayerEntry(evt.PlayerId);
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


        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_StartMatch()
        {
            if (!HasStateAuthority) return;
            if (!AllPlayersReady()) return;

            MatchStarted = true;
            EventBus.Publish(new MatchStartRequested());

            ServiceLocator.Get<Infrastructure.ISceneFlowManager>()?.LoadGameplayScene();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(int playerId, string name)
        {
            if (!HasStateAuthority) return;
            if (!TryGetPlayerIndex(playerId, out int index)) return;

            var oldEntry = Players[index];
            var newEntry = oldEntry;
            newEntry.Name = new NetworkString<_32>(SanitizeName(name, playerId));
            ReplaceEntry(oldEntry, newEntry);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetReady(int playerId, bool isReady)
        {
            if (!HasStateAuthority) return;
            if (!TryGetPlayerIndex(playerId, out int index)) return;

            var oldEntry = Players[index];
            var newEntry = oldEntry;
            newEntry.IsReady = isReady;
            ReplaceEntry(oldEntry, newEntry);

            EventBus.Publish(new PlayerReadyChanged { PlayerId = playerId, IsReady = isReady });
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

        private void ResetLobbyState()
        {
            MatchStarted = false;
            CountdownTime = 0f;

            for (int i = Players.Count - 1; i >= 0; i--)
                Players.Remove(Players[i]);

            SyncPlayersFromRunner();
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

        private bool TryGetPlayerIndex(int playerId, out int index)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId == playerId)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private void ReplaceEntry(LobbyPlayerEntry oldEntry, LobbyPlayerEntry newEntry)
        {
            Players.Remove(oldEntry);
            Players.Add(newEntry);
        }

        private static string SanitizeName(string name, int playerId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return $"Player_{playerId}";

            name = name.Trim();
            if (name.Length > 32)
                name = name.Substring(0, 32);

            return name;
        }

        private void SyncPlayersFromRunner()
        {
            foreach (var player in Runner.ActivePlayers)
                EnsurePlayerEntry(player.PlayerId);
        }

        private void EnsurePlayerEntry(int playerId)
        {
            if (TryGetPlayerIndex(playerId, out _)) return;

            string initialName = playerId == Runner.LocalPlayer.PlayerId
                ? PlayerPrefs.GetString("PlayerName", $"Player_{playerId}")
                : $"Player_{playerId}";

            Players.Add(new LobbyPlayerEntry
            {
                PlayerId  = playerId,
                IsReady   = false,
                IsHost    = Runner.IsServer && playerId == Runner.LocalPlayer.PlayerId,
                Name      = new NetworkString<_32>(SanitizeName(initialName, playerId))
            });
        }
    }

    public struct LobbyPlayerEntry : INetworkStruct
    {
        public int                  PlayerId;
        public NetworkBool          IsReady;
        public NetworkBool          IsHost;
        public NetworkString<_32>   Name;
    }

    public readonly struct LobbyListUpdated { }

}