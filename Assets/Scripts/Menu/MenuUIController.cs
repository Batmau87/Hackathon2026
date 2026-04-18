using Fusion;
using Fusion.Menu;

namespace SimpleFPS
{
    // ACTUALIZADO: Ya no usa <FusionMenuConnectArgs>
    public class MenuUIController : FusionMenuUIController
    {
        public FusionMenuConfig Config => _config;

        public GameMode SelectedGameMode { get; protected set; } = GameMode.AutoHostOrClient;

        public virtual void OnGameStarted() { }
        public virtual void OnGameStopped() { }
    }
}