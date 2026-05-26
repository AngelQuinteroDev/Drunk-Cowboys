using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FPSMultiplayer.Networking
{
    public class SkyboxApplier : MonoBehaviour
    {
        [Tooltip("Material de skybox a aplicar. Arrastralo desde el Project panel.")]
        [SerializeField] private Material skyboxMaterial;

        [Tooltip("Nombre de la escena del menu principal para evitar aplicar el skybox ahi.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool _applied;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == mainMenuSceneName) return;
            if (string.IsNullOrEmpty(scene.name)) return;
            if (scene.name == "TempActiveScene") return;

            ApplySkybox();
        }

        private void Update()
        {
            if (_applied) return;
            if (skyboxMaterial == null) return;

            var active = SceneManager.GetActiveScene();
            if (!active.IsValid()) return;
            if (active.name == mainMenuSceneName) return;
            if (active.name == "TempActiveScene") return;
            if (string.IsNullOrEmpty(active.name)) return;

            ApplySkybox();
        }

        public void ApplySkybox()
        {
            if (skyboxMaterial == null)
            {
                Debug.LogWarning("[SkyboxApplier] No skybox material assigned.");
                return;
            }

            RenderSettings.skybox      = skyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();

            _applied = true;
            Debug.Log($"[SkyboxApplier] Skybox applied: {skyboxMaterial.name} on scene: {SceneManager.GetActiveScene().name}");
        }

        public void Reset()
        {
            _applied = false;
        }
    }
}
