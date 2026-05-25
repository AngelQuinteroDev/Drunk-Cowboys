using Fusion;


namespace FPSMultiplayer.Core.Events
{
    // ── Jugadores ────────────────────────────────────────────
    public readonly struct PlayerJoinedEvent   { public int PlayerId { get; init; } }
    public readonly struct PlayerLeftEvent     { public int PlayerId { get; init; } }
    public readonly struct PlayerReadyChanged  { public int PlayerId { get; init; } public bool IsReady { get; init; } }
    public readonly struct PlayerKicked        { public int PlayerId { get; init; } }
    public readonly struct PlayerSpawned       { public int PlayerId { get; init; } }
 
    // ── Combate ──────────────────────────────────────────────
    public readonly struct PlayerDied
    {
        public int PlayerId { get; init; }
        public int KillerId { get; init; }  // -1 si fue la torreta u otro
    }
 
    public readonly struct PlayerHealthChanged { public float Health { get; init; } }
 
    // ── Rondas ───────────────────────────────────────────────
    // (Definidos en NetworkRoundManager.cs para colocación)
    // RoundStateChanged, RoundNumberChanged, RoundWinnerDeclared, MatchWinnerDeclared
 
    // ── Escenas / Sesión ─────────────────────────────────────
    public readonly struct MatchStartRequested { }
    public readonly struct SceneChangeRequest  { public string SceneName { get; init; } }
}