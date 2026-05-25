---
name: drunk-cowboys-architecture
description: 'Use when: working on Drunk Cowboys Unity FPS architecture, Fusion networking flow (MainMenu/Lobby/Gameplay), SceneFlow/SessionManager, Lobby replication, or project core systems.'
user-invocable: true
---

# Drunk Cowboys Architecture (Unity FPS)

## When to Use
- Implementing or debugging the MainMenu -> Lobby -> Gameplay flow
- Working on Photon Fusion setup (NetworkRunner, scene loading, callbacks)
- Updating lobby replication, ready/start flow, or player spawning
- Reasoning about core services (ServiceLocator, EventBus) and scene flow

## Project Overview
- Unity C# FPS game with multiplayer lobby and match start
- Networking: Photon Fusion (NetworkRunner, NetworkObject, NetworkLinkedList, RPCs)
- UI: Unity UI + TextMeshPro for MainMenu and Lobby
- Scenes (expected build order): Bootstrap (0), MainMenu (1), Lobby (2), Gameplay (3)

## Core Architecture
- **Bootstrap**
  - Creates and registers services via `ServiceLocator`.
  - Services are `DontDestroyOnLoad`.
  - Loads `MainMenu` on start.

- **SessionManager** (`ISessionManager`)
  - Owns `NetworkRunner` instance.
  - `CreateRoom` (Host) and `JoinRoom` (Client).
  - Adds `INetworkRunnerCallbacks` and handles join/leave/spawn.
  - Publishes `SceneChangeRequest` on shutdown.

- **SceneFlowManager** (`ISceneFlowManager`)
  - Listens to `SceneChangeRequest` via `EventBus`.
  - Server calls `Runner.LoadScene(SceneRef)` for Lobby/Gameplay.
  - `LoadMainMenu` shuts down runner and loads main menu scene.

- **LobbyManager** (`NetworkBehaviour`)
  - `NetworkLinkedList<LobbyPlayerEntry>` for player data.
  - Host (state authority) mutates list; clients send RPCs.
  - RPCs: `RPC_SetPlayerName`, `RPC_SetReady`, `RPC_StartMatch`, `RPC_KickPlayer`.
  - On `Spawned`: sync list from `Runner.ActivePlayers`.
  - On `Render`: publishes `LobbyListUpdated`.

- **PlayerSpawner** (`IPlayerSpawner`)
  - Server spawns player `NetworkObject` for each joined player.
  - Spawns existing players if created after runner starts.

- **UI Controllers**
  - `MainMenuUIController`: name input, create/join room.
  - `LobbyUIController`: listens to `LobbyListUpdated`, renders rows.
  - `LobbyPlayerRowUI`: binds UI row (name, ready, host, kick).

## Networking + Fusion Notes
- `NetworkProjectConfig` is in `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`.
- Peer mode is **Multiple** (value 1): scenes are merged into a MultiPeer scene.
- In Multiple mode, the active scene may stay as `MainMenu` unless set explicitly.
- Only the server should call `Runner.LoadScene`.

## Common Issues and Fixes
- **Lobby list not updating on clients**
  - Ensure `LobbyManager` is a Scene Object (`NetworkObject` marked as Scene Object).
  - Ensure `LobbyUIController` waits for `LobbyManager.Object.IsValid` before reading.

- **Scene loads only on host**
  - Server must call `Runner.LoadScene(sceneRef)`.
  - Client should not call load directly.
  - In Multiple mode, ensure a non-menu scene becomes active on load.

- **MainMenu still visible in client after load**
  - `ActiveScene` might remain `MainMenu`. Ensure active scene is switched away before unloading `MainMenu`.

- **Duplicate EventSystem/AudioListener**
  - Make sure only one EventSystem and one AudioListener are active in final scene.

## Debug Checklist
1. Verify `RunnerPrefab` assigned in `Bootstrap`.
2. Confirm `LobbyManager` is a Scene Object in the Lobby scene.
3. Ensure `Runner.LoadScene` is called only on server.
4. Check `SessionManager.OnSceneLoadStart/Done` logs on clients.
5. Verify `ActiveScene` is not `MainMenu` after scene load in Multiple mode.

## Key Files
- `Assets/scripts/Infraestructure/Bootstrap.cs`
- `Assets/scripts/Networking/SessionManager.cs`
- `Assets/scripts/Infraestructure/SceneFlowManager.cs`
- `Assets/scripts/Lobby/LobbyManager.cs`
- `Assets/scripts/Networking/PlayerSpawner.cs`
- `Assets/scripts/UI/MainMenuUIController.cs`
- `Assets/scripts/UI/LobbyUIController.cs`
- `Assets/scripts/UI/LobbyPlayerRowUI.cs`
- `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`
