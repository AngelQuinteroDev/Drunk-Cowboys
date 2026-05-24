// Estado replicado de cada jugador. Todo [Networked] se sincroniza automáticamente.
// Este componente va en el prefab del jugador.
using Fusion;
using UnityEngine;

namespace FPSMultiplayer.Gameplay
{
    public class NetworkPlayerData : NetworkBehaviour
    {
        // ─── Estado replicado ─────────────────────────────────────────────────
        // [Networked] solo puede ser leído por todos, pero modificado SOLO por State Authority (host).
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public int   TeamId    { get; set; }
        [Networked] public float Health    { get; set; } = 100f;
        [Networked] public bool  IsAlive   { get; set; } = true;
        [Networked] public bool  IsReady   { get; set; }
        [Networked] public int   Kills     { get; set; }
        [Networked] public int   Deaths    { get; set; }

        // ─── Cambio de detectores (ChangeDetector es el patrón correcto en Fusion 2) ──
        private ChangeDetector _changes;
        private float _lastHealth;

        public override void Spawned()
        {
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _lastHealth = Health;

            // El cliente con InputAuthority inicializa su nombre
            if (HasInputAuthority)
            {
                RPC_SetPlayerName(PlayerPrefs.GetString("PlayerName", $"Player_{Object.InputAuthority.PlayerId}"));
            }
        }

        public override void Render()
        {
            // Render() corre en todos los clientes. Detectar cambios aquí para UI.
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
            // Notificar UI local si es nuestro jugador
            if (HasInputAuthority)
                Core.EventBus.Publish(new Core.Events.PlayerHealthChanged { Health = newHealth });
        }

        private void OnAliveStateChanged(bool isAlive)
        {
            if (!isAlive && HasInputAuthority)
                Core.EventBus.Publish(new Core.Events.PlayerDied { PlayerId = Object.InputAuthority.PlayerId, KillerId = -1 });
        }

        // ─── RPCs ─────────────────────────────────────────────────────────────
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(string name)
        {
            // Solo State Authority (host) puede escribir propiedades Networked
            PlayerName = name;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_ToggleReady()
        {
            IsReady = !IsReady;
        }
    }
}