// ============================================================
//  NetworkRoundManager — Fusion 2, Player Host Topology
// ============================================================
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using FPSMultiplayer.Core;
using FPSMultiplayer.Core.Events;
using FPSMultiplayer.Networking;

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
        [SerializeField] private int   maxRounds          = 5;
        [SerializeField] private int   winsToWin          = 3;
        [SerializeField] private float countdownTime      = 3f;
        [SerializeField] private float roundEndDelay      = 4f;
        [SerializeField] private float returnToLobbyDelay = 6f;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Auto Start")]
        [SerializeField] private bool  autoStartMatch   = true;
        [SerializeField] private float autoStartDelay   = 1f;
        [SerializeField, Min(1)] private int minPlayersToStart = 2;

        [Header("Debug")]
        [SerializeField] private bool  logWaitingState    = false;
        [SerializeField] private float waitingLogInterval = 2f;

        [Header("Alcohol Bottles")]
        [SerializeField] private NetworkObject alcoholBottlePrefab;
        [SerializeField] private Transform[]   alcoholSpawnPoints;
        [SerializeField, Min(0)] private int   bottlesPerRound = 6;

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
        private TickTimer      _autoStartTimer;
        private TickTimer      _waitingLogTimer;
        private ChangeDetector _changes;
        private readonly HashSet<PlayerRef>  _alivePlayers   = new();
        private readonly List<NetworkObject> _spawnedBottles = new();
        private readonly List<int>           _bottleSpawnOrder = new();
        private bool _autoStartArmed;
        private int  _roundStartAliveCount;

        public override void Spawned()
        {
            ServiceLocator.Register(this);
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (HasStateAuthority)
            {
                CurrentState = RoundState.WaitingToStart;

                if (autoStartMatch)
                {
                    _autoStartTimer = TickTimer.CreateFromSeconds(Runner, autoStartDelay);
                    _autoStartArmed = true;
                }
            }

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

            if (autoStartMatch && _autoStartArmed && CurrentState == RoundState.WaitingToStart)
            {
                if (_autoStartTimer.Expired(Runner) && HasEnoughPlayers())
                {
                    BeginMatch();
                    _autoStartArmed = false;
                }
            }

            if (CurrentState == RoundState.WaitingToStart)
                LogWaitingState();

            switch (CurrentState)
            {
                case RoundState.Countdown:
                    CountdownSeconds = _phaseTimer.RemainingTime(Runner) ?? 0f;
                    if (_phaseTimer.Expired(Runner))
                        CurrentState = RoundState.Active;
                    break;

                case RoundState.Active:
                    if (_roundStartAliveCount > 0 && AliveCount <= 1)
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
                                EventBus.Publish(new RoundWinnerDeclared
                                {
                                    Winner = LastRoundWinner,
                                    WinnerName = ResolvePlayerDisplayName(LastRoundWinner)
                                });
                        break;
                    case nameof(MatchWinner):
                            EventBus.Publish(new MatchWinnerDeclared
                            {
                                Winner = MatchWinner,
                                WinnerName = ResolveMatchWinnerDisplayName(MatchWinner)
                            });
                        break;
                }
            }
        }

        public void BeginMatch()
        {
            if (!HasStateAuthority) return;
            if (CurrentState != RoundState.WaitingToStart) return;
            if (!HasEnoughPlayers())
            {
                ArmAutoStart();
                return;
            }
            CurrentRound = 0;
            RoundWins.Clear();
            StartRound();
        }

        private void StartRound()
        {
            if (!HasEnoughPlayers())
            {
                CurrentState     = RoundState.WaitingToStart;
                CountdownSeconds = 0f;
                _phaseTimer      = default;
                DespawnBottles();
                ArmAutoStart();
                return;
            }

            CurrentRound++;
            LastRoundWinner  = PlayerRef.None;
            CurrentState     = RoundState.Countdown;
            CountdownSeconds = countdownTime;
            _phaseTimer      = TickTimer.CreateFromSeconds(Runner, countdownTime);

            RespawnAllPlayers();   // <-- ahora resetea health ANTES de mover al player
            DespawnBottles();
            SpawnBottles();
            BuildAliveSet();
            AliveCount            = _alivePlayers.Count;
            _roundStartAliveCount = AliveCount;

            Debug.Log($"[RoundManager] Ronda {CurrentRound}/{maxRounds} — countdown {countdownTime}s");
        }

        private void DeclareRoundWinner()
        {
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
            Debug.Log($"[RoundManager] Ronda {CurrentRound} terminada. Ganador: {ResolvePlayerDisplayName(winner)}");
        }

        private void StartNextRoundOrEnd()
        {
            if (!HasEnoughPlayers())
            {
                CurrentState     = RoundState.WaitingToStart;
                CountdownSeconds = 0f;
                _phaseTimer      = default;
                ArmAutoStart();
                return;
            }

            PlayerRef matchWinner = ResolveMatchWinner();

            if (matchWinner != PlayerRef.None || CurrentRound >= maxRounds)
            {
                MatchWinner  = matchWinner;
                CurrentState = RoundState.MatchEnded;
                _phaseTimer  = TickTimer.CreateFromSeconds(Runner, returnToLobbyDelay);
                Debug.Log($"[RoundManager] Partida terminada. Ganador: {ResolveMatchWinnerDisplayName(matchWinner)}");
            }
            else
            {
                StartRound();
            }
        }

        private PlayerRef ResolveMatchWinner()
        {
            foreach (var kvp in RoundWins)
                if (kvp.Value >= winsToWin) return kvp.Key;

            if (CurrentRound >= maxRounds)
            {
                PlayerRef best = PlayerRef.None;
                int bestWins   = -1;
                bool tie       = false;

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
            DespawnBottles();
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

                // ResetForRound hace internamente:
                //   Teleport → ForceRespawn (dispara OnRespawn en todos los clientes
                //   via ChangeDetector de HealthSystem) → ResetAmmo → ResetDrunk
                // No llamar ForceRespawn aqui por separado para evitar doble disparo
                // del evento OnRespawn que confunde al Animator.
                Transform spawn = GetSpawnPoint(index);
                pc.ResetForRound(spawn.position, spawn.rotation);
                index++;
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

        public string GetPlayerDisplayName(PlayerRef playerRef)
        {
            return ResolvePlayerDisplayName(playerRef);
        }

        private string ResolveMatchWinnerDisplayName(PlayerRef playerRef)
        {
            if (playerRef == PlayerRef.None)
                return "Empate";

            return ResolvePlayerDisplayName(playerRef);
        }

        private string ResolvePlayerDisplayName(PlayerRef playerRef)
        {
            if (playerRef == PlayerRef.None)
                return "Sin ganador";

            var playerObject = Runner.GetPlayerObject(playerRef);
            if (playerObject != null && playerObject.TryGetComponent<PlayerController>(out var playerController))
            {
                string playerName = playerController.PlayerName.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(playerName))
                    return playerName;
            }

            if (playerObject != null && playerObject.TryGetComponent<NetworkPlayerData>(out var playerData))
            {
                string playerName = playerData.PlayerName.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(playerName))
                    return playerName;
            }

            return $"Player_{playerRef.PlayerId}";
        }

        private bool HasEnoughPlayers()
        {
            int count = 0;

            if (!ServiceLocator.TryGet<IPlayerSpawner>(out var spawner))
                spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();

            if (spawner != null)
            {
                foreach (var player in Runner.ActivePlayers)
                {
                    if (Runner.GetPlayerObject(player) == null)
                        spawner.SpawnPlayer(Runner, player);
                }
            }

            foreach (var player in Runner.ActivePlayers)
            {
                var obj = Runner.GetPlayerObject(player);
                if (obj != null && obj.IsValid)
                    count++;

                if (count >= minPlayersToStart)
                    return true;
            }

            return false;
        }

        private void LogWaitingState()
        {
            if (!logWaitingState) return;
            if (!_waitingLogTimer.ExpiredOrNotRunning(Runner)) return;

            _waitingLogTimer = TickTimer.CreateFromSeconds(Runner, waitingLogInterval);

            int activePlayers = 0;
            int playerObjects = 0;

            foreach (var player in Runner.ActivePlayers)
            {
                activePlayers++;
                var obj = Runner.GetPlayerObject(player);
                if (obj != null && obj.IsValid) playerObjects++;
            }

            int    missingObjects = activePlayers - playerObjects;
            string reason;

            if (!autoStartMatch)          reason = "autoStartMatch disabled";
            else if (!_autoStartArmed)    reason = "autoStart not armed";
            else if (activePlayers < minPlayersToStart)
                reason = $"activePlayers({activePlayers}) < minPlayersToStart({minPlayersToStart})";
            else if (playerObjects < minPlayersToStart)
                reason = $"playerObjects({playerObjects}) < minPlayersToStart({minPlayersToStart})";
            else if (!_autoStartTimer.Expired(Runner))
                reason = $"autoStartDelay running ({autoStartDelay}s)";
            else
                reason = "eligible to start; check spawner or authority";

            Debug.Log(
                $"[RoundManager] WaitingToStart: activePlayers={activePlayers}, " +
                $"playerObjects={playerObjects}, missingPlayerObjects={missingObjects}, reason={reason}");
        }

        private void ArmAutoStart()
        {
            if (!autoStartMatch) return;
            _autoStartTimer = TickTimer.CreateFromSeconds(Runner, autoStartDelay);
            _autoStartArmed = true;
        }

        private void SpawnBottles()
        {
            if (alcoholBottlePrefab == null || alcoholSpawnPoints == null || alcoholSpawnPoints.Length == 0) return;
            if (bottlesPerRound <= 0) return;

            _bottleSpawnOrder.Clear();
            for (int i = 0; i < alcoholSpawnPoints.Length; i++)
                _bottleSpawnOrder.Add(i);

            int count = Mathf.Min(bottlesPerRound, _bottleSpawnOrder.Count);
            for (int i = 0; i < count; i++)
            {
                int swapIndex = Random.Range(i, _bottleSpawnOrder.Count);
                int chosen    = _bottleSpawnOrder[swapIndex];
                _bottleSpawnOrder[swapIndex] = _bottleSpawnOrder[i];
                _bottleSpawnOrder[i]         = chosen;

                Transform spawn = alcoholSpawnPoints[chosen];
                if (spawn == null) continue;

                var bottle = Runner.Spawn(alcoholBottlePrefab, spawn.position, spawn.rotation);
                if (bottle != null) _spawnedBottles.Add(bottle);
            }
        }

        private void DespawnBottles()
        {
            if (!HasStateAuthority) return;

            for (int i = _spawnedBottles.Count - 1; i >= 0; i--)
            {
                var bottle = _spawnedBottles[i];
                if (bottle != null && bottle.IsValid)
                    Runner.Despawn(bottle);
            }

            _spawnedBottles.Clear();
        }
    }

    // Eventos para el EventBus — UI escucha estos
    public readonly struct RoundStateChanged   { public RoundState State  { get; init; } }
    public readonly struct RoundNumberChanged  { public int        Round  { get; init; } }
    public readonly struct RoundWinnerDeclared { public PlayerRef Winner { get; init; } public string WinnerName { get; init; } }
    public readonly struct MatchWinnerDeclared { public PlayerRef Winner { get; init; } public string WinnerName { get; init; } }
}