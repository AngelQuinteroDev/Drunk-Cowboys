using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSMultiplayer.Shared
{
    [Serializable]
    public class SceneReference : ISerializationCallbackReceiver
    {
        [SerializeField] private string _scenePath;

#if UNITY_EDITOR
        [SerializeField] private UnityEditor.SceneAsset _sceneAsset;
#endif

        public string ScenePath => _scenePath;

        public SceneRef ToSceneRef()
        {
            int buildIndex = string.IsNullOrEmpty(_scenePath)
                ? -1
                : SceneUtility.GetBuildIndexByScenePath(_scenePath);

            return buildIndex >= 0 ? SceneRef.FromIndex(buildIndex) : SceneRef.None;
        }

        public static implicit operator SceneRef(SceneReference reference)
        {
            return reference != null ? reference.ToSceneRef() : SceneRef.None;
        }

#if UNITY_EDITOR
        public void OnBeforeSerialize()
        {
            if (_sceneAsset == null) return;

            string path = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
            if (!string.Equals(_scenePath, path, StringComparison.Ordinal))
                _scenePath = path;
        }

        public void OnAfterDeserialize() { }
#else
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() { }
#endif
    }
}
