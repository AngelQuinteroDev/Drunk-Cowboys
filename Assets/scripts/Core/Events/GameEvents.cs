namespace FPSMultiplayer.Core.Events
{
    public readonly struct PlayerJoinedEvent   { public int PlayerId { get; init; } }
    public readonly struct PlayerLeftEvent     { public int PlayerId { get; init; } }
    public readonly struct PlayerReadyChanged  { public int PlayerId { get; init; } public bool IsReady { get; init; } }
    public readonly struct MatchStartRequested { }
    public readonly struct SceneChangeRequest  { public string SceneName { get; init; } }
    public readonly struct PlayerKicked        { public int PlayerId { get; init; } }
    public readonly struct PlayerSpawned       { public int PlayerId { get; init; } }
    public readonly struct PlayerDied          { public int PlayerId { get; init; } public int KillerId { get; init; } }
    public readonly struct PlayerHealthChanged { public float Health { get; init; } }
}