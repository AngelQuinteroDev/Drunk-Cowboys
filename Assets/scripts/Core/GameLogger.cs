using UnityEngine;

namespace FPSMultiplayer.Core
{
    public class GameLogger
    {
        public void Log(string message) => Debug.Log(message);
        public void Warn(string message) => Debug.LogWarning(message);
        public void Error(string message) => Debug.LogError(message);
    }
}
