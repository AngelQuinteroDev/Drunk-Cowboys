using UnityEngine;
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
            if (_runner == null || !_runner.IsServer) return;
            _runner.LoadScene(_gameplayScene);
        }

        public void LoadLobbyScene()
        {
            _runner = ServiceLocator.Get<ISessionManager>()?.Runner;
            if (_runner != null && _runner.IsServer)
                _runner.LoadScene(_lobbyScene);
        }

        public void LoadMainMenu()
        {
            _runner = ServiceLocator.Get<ISessionManager>()?.Runner;
            _runner?.Shutdown();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex(0).name);
        }
    }
}