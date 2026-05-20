using UnityEngine;

namespace FPSMultiplayer.Infrastructure
{
    public interface IMatchManager
    {
        bool IsMatchRunning { get; }
        void StartMatch();
        void EndMatch();
    }

    public class MatchManager : MonoBehaviour, IMatchManager
    {
        public bool IsMatchRunning { get; private set; }

        public void StartMatch()
        {
            IsMatchRunning = true;
        }

        public void EndMatch()
        {
            IsMatchRunning = false;
        }
    }
}
