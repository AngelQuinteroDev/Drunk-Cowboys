// ============================================================
//  NetworkRoundManager — Fusion 2, Player Host Topology
//
//  LÓGICA DE RONDAS:
//    - Ronda inicia cuando el host llama BeginMatch()
//    - Jugadores se teleportan a spawn points y se resetean
//    - Ronda termina cuando queda <= 1 jugador vivo
//    - Se registra la victoria y pausa antes de la siguiente
//    - Tras maxRounds o al llegar a winsToWin, se declara
//      ganador de partida y se regresa al lobby
//
//  RESPONSABILIDADES:
//    HOST  : toda la lógica de rondas, conteo de vivos, victoria
//    TODOS : leer estado replicado para UI via ChangeDetector
// ============================================================
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;

namespace FPSMultiplayer.Gameplay
{
    public enum RoundState : byte
    {
        WaitingToStart,
        Countdown,
        Active,
        RoundEnded,
        MatchEnded
    }

    public class NetworkRoundManager : NetworkBehaviour
    {
        [Header("Configuracion de Partida")]
        [SerializeField] private int   maxRounds         = 5;
        [SerializeField] private int   winsToWin         = 3;
        [SerializeField] private float countdownTime     = 3f;
        [SerializeField] private float roundEndDelay     = 4f;
        [SerializeField] private float returnToLobbyDelay = 6f;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        // Estado replicado
        [Networked] public RoundState CurrentState     { get; private set; }
        [Networked] public int        CurrentRound     { get; private set; }
        [Networked] public int        AliveCount       { get; private set; }
        [Networked] public float      CountdownSeconds { get; private set; }
        [Networked] public PlayerRef  LastRoundWinner  { get; private set; }
        [Networked] public PlayerRef  MatchWinner      { get; private set; }

        [Networked, Capacity(8)]
        public NetworkDictionary<PlayerRef, int> RoundWins { get; }

        public bool IsRoundActive => CurrentState == RoundState.Active;

        private TickTimer      _phaseTimer;
        private ChangeDetector _changes;
        private readonly HashSet<PlayerRef> _alivePlayers = new();

        public override void Spawned()
        {
            ServiceLocator.Register(this);
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (HasStateAuthority)
                CurrentState = RoundState.WaitingToStart;

            EventBus.Subscribe<PlayerDied>(OnPlayerDied);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ServiceLocator.Unregister<NetworkRoundManager>();
            EventBus.Unsubscribe<PlayerDied>(OnPlayerDied);
        }

        // HOST ONLY
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (CurrentState)
            {
                case RoundState.Countdown:
                    CountdownSeconds = _phaseTimer.RemainingTime(Runner) ?? 0f;
                    if (_phaseTimer.Expired(Runner))
                        CurrentState = RoundState.Active;
                    break;

                case RoundState.Active:
                    if (AliveCount <= 1)
                        DeclareRoundWinner();
                    break;

                case RoundState.RoundEnded:
                    if (_phaseTimer.Expired(Runner))
                        StartNextRoundOrEnd();
                    break;

                case RoundState.MatchEnded:
                    if (_phaseTimer.Expired(Runner))
                        ReturnToLobby();
                    break;
            }
        }

        // TODOS — detecta cambios y publica eventos para UI
        public override void Render()
        {
            foreach (var change in _changes.DetectChanges(this, out _, out _))
            {
                switch (change)
                {
                    case nameof(CurrentState):
                        EventBus.Publish(new RoundStateChanged { State = CurrentState });
                        break;
                    case nameof(CurrentRound):
                        EventBus.Publish(new RoundNumberChanged { Round = CurrentRound });
                        break;
                    case nameof(LastRoundWinner):
                        if (LastRoundWinner != PlayerRef.None)
                            EventBus.Publish(new RoundWinnerDeclared { Winner = LastRoundWinner });
                        break;
                    case nameof(MatchWinner):
                        EventBus.Publish(new MatchWinnerDeclared { Winner = MatchWinner });
                        break;
                }
            }
        }

        // Llamado por el host al cargar la escena Gameplay
        public void BeginMatch()
        {
            if (!HasStateAuthority) return;
            if (CurrentState != RoundState.WaitingToStart) return;
            CurrentRound = 0;
            RoundWins.Clear();
            StartRound();
        }

        private void StartRound()
        {
            CurrentRound++;
            LastRoundWinner  = PlayerRef.None;
            CurrentState     = RoundState.Countdown;
            CountdownSeconds = countdownTime;
            _phaseTimer      = TickTimer.CreateFromSeconds(Runner, countdownTime);

            RespawnAllPlayers();
            BuildAliveSet();
            AliveCount = _alivePlayers.Count;

            Debug.Log($"[RoundManager] Ronda {CurrentRound}/{maxRounds} — countdown {countdownTime}s");
        }

        private void DeclareRoundWinner()
        {
            // Encontrar el ultimo vivo
            PlayerRef winner = PlayerRef.None;
            foreach (var p in _alivePlayers) { winner = p; break; }

            LastRoundWinner = winner;
            CurrentState    = RoundState.RoundEnded;

            if (winner != PlayerRef.None)
            {
                RoundWins.TryGet(winner, out int current);
                RoundWins.Set(winner, current + 1);

                var obj = Runner.GetPlayerObject(winner);
                if (obj != null && obj.TryGetComponent<PlayerController>(out var pc))
                    pc.RoundWins++;
            }

            _phaseTimer = TickTimer.CreateFromSeconds(Runner, roundEndDelay);
            Debug.Log($"[RoundManager] Ronda {CurrentRound} terminada. Ganador: {winner}");
        }

        private void StartNextRoundOrEnd()
        {
            PlayerRef matchWinner = ResolveMatchWinner();

            if (matchWinner != PlayerRef.None || CurrentRound >= maxRounds)
            {
                MatchWinner  = matchWinner;
                CurrentState = RoundState.MatchEnded;
                _phaseTimer  = TickTimer.CreateFromSeconds(Runner, returnToLobbyDelay);
                Debug.Log($"[RoundManager] Partida terminada. Ganador: {matchWinner}");
            }
            else
            {
                StartRound();
            }
        }

        private PlayerRef ResolveMatchWinner()
        {
            // Alguien llego a winsToWin
            foreach (var kvp in RoundWins)
                if (kvp.Value >= winsToWin) return kvp.Key;

            // Se agotaron las rondas: gana el que mas tiene, si no hay empate
            if (CurrentRound >= maxRounds)
            {
                PlayerRef best  = PlayerRef.None;
                int bestWins    = -1;
                bool tie        = false;

                foreach (var kvp in RoundWins)
                {
                    if (kvp.Value > bestWins)
                    {
                        best = kvp.Key; bestWins = kvp.Value; tie = false;
                    }
                    else if (kvp.Value == bestWins)
                    {
                        tie = true;
                    }
                }
                return tie ? PlayerRef.None : best;
            }

            return PlayerRef.None;
        }

        private void ReturnToLobby()
        {
            var sceneFlow = ServiceLocator.Get<FPSMultiplayer.Infrastructure.ISceneFlowManager>();
            sceneFlow?.LoadLobbyScene();
        }

        private void RespawnAllPlayers()
        {
            int index = 0;
            foreach (var player in Runner.ActivePlayers)
            {
                var obj = Runner.GetPlayerObject(player);
                if (obj == null || !obj.TryGetComponent<PlayerController>(out var pc)) continue;

                Transform spawn = GetSpawnPoint(index++);
                pc.ResetForRound(spawn.position, spawn.rotation);
            }
        }

        private void BuildAliveSet()
        {
            _alivePlayers.Clear();
            foreach (var player in Runner.ActivePlayers)
            {
                var obj    = Runner.GetPlayerObject(player);
                var health = obj?.GetComponent<HealthSystem>();
                if (health != null && health.IsAlive)
                    _alivePlayers.Add(player);
            }
        }

        private Transform GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return transform;
            return spawnPoints[index % spawnPoints.Length];
        }

        private void OnPlayerDied(PlayerDied evt)
        {
            if (!HasStateAuthority) return;
            if (CurrentState != RoundState.Active) return;

            foreach (var player in Runner.ActivePlayers)
            {
                if (player.PlayerId == evt.PlayerId)
                {
                    _alivePlayers.Remove(player);
                    break;
                }
            }

            AliveCount = _alivePlayers.Count;
            Debug.Log($"[RoundManager] Player {evt.PlayerId} murio. Vivos restantes: {AliveCount}");
        }

        public void OnPlayerDisconnected(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            _alivePlayers.Remove(player);
            AliveCount = _alivePlayers.Count;

            if (CurrentState == RoundState.Active && AliveCount <= 1)
                DeclareRoundWinner();
        }

        public int GetWins(PlayerRef player)
        {
            RoundWins.TryGet(player, out int wins);
            return wins;
        }
    }

    // Eventos para el EventBus — UI escucha estos
    public readonly struct RoundStateChanged   { public RoundState State  { get; init; } }
    public readonly struct RoundNumberChanged  { public int        Round  { get; init; } }
    public readonly struct RoundWinnerDeclared { public PlayerRef  Winner { get; init; } }
    public readonly struct MatchWinnerDeclared { public PlayerRef  Winner { get; init; } }
}