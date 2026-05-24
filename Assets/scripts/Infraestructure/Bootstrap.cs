using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using FPSMultiplayer.Core;
using FPSMultiplayer.Networking;
using FPSMultiplayer.Infrastructure;

namespace FPSMultiplayer.Infrastructure
{
    public class Bootstrap : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string _mainMenuScene = "MainMenu";

        [Header("Networking")]
        [SerializeField] private NetworkRunner _runnerPrefab;

        private void Awake()
        {
            // Orden de inicialización importa: primero logger, luego servicios de red.
            var logger = new GameLogger();
            ServiceLocator.Register(logger);

            var sessionManager = new GameObject("SessionManager").AddComponent<SessionManager>();
            sessionManager.Configure(_runnerPrefab);
            DontDestroyOnLoad(sessionManager.gameObject);
            ServiceLocator.Register<ISessionManager>(sessionManager);

            var sceneFlow = new GameObject("SceneFlowManager").AddComponent<SceneFlowManager>();
            DontDestroyOnLoad(sceneFlow.gameObject);
            ServiceLocator.Register<ISceneFlowManager>(sceneFlow);

            var matchManager = new GameObject("MatchManager").AddComponent<MatchManager>();
            DontDestroyOnLoad(matchManager.gameObject);
            ServiceLocator.Register<IMatchManager>(matchManager);

            logger.Log("[Bootstrap] All services initialized.");
            SceneManager.LoadScene(_mainMenuScene);
        }
    }
}