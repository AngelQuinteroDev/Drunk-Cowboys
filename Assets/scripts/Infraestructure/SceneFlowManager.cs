using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using FPSMultiplayer.Core;
using FPSMultiplayer.Networking;
using FPSMultiplayer.Shared;

namespace FPSMultiplayer.Infrastructure
{
    public interface ISceneFlowManager
    {
        void LoadGameplayScene();
        void LoadLobbyScene();
        void LoadMainMenu();
    }

    public class SceneFlowManager : MonoBehaviour, ISceneFlowManager
    {
        [SerializeField] private SceneReference _lobbyScene;
        [SerializeField] private SceneReference _gameplayScene;
        [SerializeField] private SceneReference _mainMenuScene;

        [Header("Fallback Names")]
        [SerializeField] private string _lobbySceneName = GameConstants.Scene.Lobby;
        [SerializeField] private string _gameplaySceneName = GameConstants.Scene.Gameplay;
        [SerializeField] private string _mainMenuSceneName = GameConstants.Scene.MainMenu;

        private NetworkRunner _runner;

        private void Start()
        {
            EventBus.Subscribe<Core.Events.SceneChangeRequest>(OnSceneChangeRequest);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<Core.Events.SceneChangeRequest>(OnSceneChangeRequest);
        }

        private void OnSceneChangeRequest(Core.Events.SceneChangeRequest evt)
        {
            Debug.Log($"[SceneFlowManager] SceneChangeRequest received: {evt.SceneName}");
            switch (evt.SceneName)
            {
                case GameConstants.Scene.Lobby:    LoadLobbyScene();    break;
                case GameConstants.Scene.Gameplay: LoadGameplayScene(); break;
                case GameConstants.Scene.MainMenu: LoadMainMenu();      break;
            }
        }

        public void LoadGameplayScene()
        {
            _runner = ServiceLocator.Get<ISessionManager>()?.Runner;
            if (_runner == null)
            {
                Debug.LogError("[SceneFlowManager] Runner is null when trying to load gameplay scene.");
                return;
            }

            ServiceLocator.TryGet<ILoadingScreenService>(out var loadingScreen);
            loadingScreen?.Show("Iniciando partida");

            Debug.Log($"[SceneFlowManager] Request to load gameplay scene. Runner.IsServer={_runner.IsServer}, Runner={_runner}");

            if (!_runner.IsServer)
            {
                Debug.LogWarning("[SceneFlowManager] Only server should invoke scene loads.");
                loadingScreen?.Hide();
                return;
            }

            var sceneRef = ResolveSceneRef(_gameplayScene, _gameplaySceneName);
            if (sceneRef == default)
            {
                Debug.LogError("[SceneFlowManager] Resolved sceneRef is invalid for gameplay scene.");
                loadingScreen?.Hide();
                return;
            }

            Debug.Log($"[SceneFlowManager] Invoking Runner.LoadScene for '{_gameplaySceneName}'.");
            _runner.LoadScene(sceneRef);
        }

        public void LoadLobbyScene()
        {
            _runner = ServiceLocator.Get<ISessionManager>()?.Runner;
            ServiceLocator.TryGet<ILoadingScreenService>(out var loadingScreen);
            loadingScreen?.Show("Volviendo al lobby");
            if (_runner != null && _runner.IsServer)
            {
                var sceneRef = ResolveSceneRef(_lobbyScene, _lobbySceneName);
                if (sceneRef == default)
                {
                    loadingScreen?.Hide();
                    return;
                }
                _runner.LoadScene(sceneRef);
            }
            else
            {
                loadingScreen?.Hide();
            }
        }

        public void LoadMainMenu()
        {
            Debug.Log("[SceneFlowManager] LoadMainMenu invoked.");
            _runner = ServiceLocator.Get<ISessionManager>()?.Runner;

            ServiceLocator.TryGet<ILoadingScreenService>(out var loadingScreen);
            loadingScreen?.Show("Volviendo al menu", hideOnNextSceneLoaded: true);

            if (_runner != null)
            {
                Debug.Log($"[SceneFlowManager] Shutting down runner. IsServer={_runner.IsServer}");
                _runner.Shutdown();
            }

            if (!TryLoadUnityScene(_mainMenuScene, _mainMenuSceneName))
            {
                SceneManager.LoadScene(SceneManager.GetSceneByBuildIndex(0).name);
            }
        }

        private SceneRef ResolveSceneRef(SceneReference reference, string fallbackName)
        {
            if (reference != null)
            {
                var sceneRef = reference.ToSceneRef();
                if (sceneRef != default)
                    return sceneRef;
            }

            if (TryGetSceneRefByName(fallbackName, out var fallbackRef))
                return fallbackRef;

            Debug.LogError($"[SceneFlowManager] Scene '{fallbackName}' is invalid or not in Build Settings.");
            return default;
        }

        private static bool TryGetSceneRefByName(string sceneName, out SceneRef sceneRef)
        {
            sceneRef = default;
            if (string.IsNullOrWhiteSpace(sceneName)) return false;

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    sceneRef = SceneRef.FromIndex(i);
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadUnityScene(SceneReference reference, string fallbackName)
        {
            string sceneName = null;

            if (reference != null && !string.IsNullOrEmpty(reference.ScenePath))
                sceneName = Path.GetFileNameWithoutExtension(reference.ScenePath);

            if (string.IsNullOrWhiteSpace(sceneName))
                sceneName = fallbackName;

            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}