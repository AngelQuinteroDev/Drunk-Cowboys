using Fusion;
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    public class NetworkPlayerData : NetworkBehaviour
    {
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public int   TeamId    { get; set; }
        [Networked] public float Health    { get; set; } = 100f;
        [Networked] public bool  IsAlive   { get; set; } = true;
        [Networked] public bool  IsReady   { get; set; }
        [Networked] public int   Kills     { get; set; }
        [Networked] public int   Deaths    { get; set; }

        private ChangeDetector _changes;
        private float _lastHealth;

        public override void Spawned()
        {
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _lastHealth = Health;

            if (HasInputAuthority)
            {
                RPC_SetPlayerName(PlayerPrefs.GetString("PlayerName", $"Player_{Object.InputAuthority.PlayerId}"));
            }
        }

        public override void Render()
        {
            foreach (var change in _changes.DetectChanges(this, out _, out _))
            {
                switch (change)
                {
                    case nameof(Health):
                        OnHealthChanged(_lastHealth, Health);
                        _lastHealth = Health;
                        break;
                    case nameof(IsAlive):
                        OnAliveStateChanged(IsAlive);
                        break;
                }
            }
        }

        private void OnHealthChanged(float oldHealth, float newHealth)
        {
            if (HasInputAuthority)
                Core.EventBus.Publish(new Core.Events.PlayerHealthChanged { Health = newHealth });
        }

        private void OnAliveStateChanged(bool isAlive)
        {
            if (!isAlive && HasInputAuthority)
                Core.EventBus.Publish(new Core.Events.PlayerDied { PlayerId = Object.InputAuthority.PlayerId, KillerId = -1 });
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(string name)
        {
            PlayerName = name;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_ToggleReady()
        {
            IsReady = !IsReady;
        }
    }
}