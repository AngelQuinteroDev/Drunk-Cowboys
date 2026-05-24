// Constantes centralizadas. No magic strings dispersas en el proyecto.
namespace FPSMultiplayer.Shared
{
    public static class GameConstants
    {
        public static class Scene
        {
            public const string Bootstrap = "Bootstrap";
            public const string MainMenu  = "MainMenu";
            public const string Lobby     = "Lobby";
            public const string Gameplay  = "Gameplay";
        }

        public static class Network
        {
            public const int   MaxPlayers       = 8;
            public const float TickRate          = 60f;
            public const int   DefaultPort       = 27015;
        }

        public const float MouseSensitivity = 0.15f;
        public const float RespawnDelay     = 3f;
    }
}