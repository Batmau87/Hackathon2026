using Fusion;
using TMPro;
using UnityEngine;

namespace HackathonJuego
{
    public class InGameLobbyUI : MonoBehaviour
    {
        [Header("Paneles")]
        public GameObject mainLobbyPanel;

        [Header("Textos")]
        public TextMeshProUGUI sessionCodeText;
        public TextMeshProUGUI playerCountText;
        public TextMeshProUGUI statusText;

        [Header("Botones")]
        public GameObject startGameButton;
        public GameObject readyButton;

        private Gameplay _gameplay;
        private NetworkRunner _runner;

        private void Start()
        {
            SetLobbyVisible(true);
        }

        private void Update()
        {
            if (_runner == null)
                _runner = FindFirstObjectByType<NetworkRunner>();

            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();

            if (_runner == null || !_runner.IsRunning || _gameplay == null)
                return;

            if (_gameplay.Object == null || !_gameplay.Object.IsValid)
                return;

            bool inLobby = _gameplay.State == EGameplayState.Lobby;
            SetLobbyVisible(inLobby);

            if (!inLobby)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            int connectedPlayers = 0;
            bool allReady = true;

            foreach (var pair in _gameplay.PlayerData)
            {
                PlayerData data = pair.Value;
                if (!data.IsConnected)
                    continue;

                connectedPlayers++;
                if (!data.IsReady)
                    allReady = false;
            }

            if (sessionCodeText != null)
            {
                string sessionName = (_runner.SessionInfo != null && _runner.SessionInfo.IsValid)
                    ? _runner.SessionInfo.Name
                    : "Conectando...";

                sessionCodeText.text = $"CODIGO: {sessionName}";
            }

            if (playerCountText != null)
                playerCountText.text = $"Jugadores: {connectedPlayers} / 3";

            if (statusText != null)
            {
                if (_gameplay.PlayerData.TryGet(_runner.LocalPlayer, out var myData))
                {
                    string readyState = myData.IsReady
                        ? "<color=green>LISTO</color>"
                        : "<color=yellow>ESPERANDO...</color>";

                    statusText.text = $"Estado: {readyState}";
                }
                else
                {
                    statusText.text = "Estado: <color=yellow>REGISTRANDO JUGADOR...</color>";
                }
            }

            if (readyButton != null)
                readyButton.SetActive(true);

            if (startGameButton != null)
            {
                bool isHost = _runner.IsServer || _runner.IsSharedModeMasterClient;
                bool enoughPlayers = connectedPlayers >= 3;
                startGameButton.SetActive(isHost && enoughPlayers && allReady);
            }
        }

        public void Btn_StartGame()
        {
            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();

            if (_gameplay == null || _gameplay.State != EGameplayState.Lobby)
                return;

            _gameplay.StartMatchFromUI();
        }

        public void Btn_ToggleReady()
        {
            if (_runner == null)
                _runner = FindFirstObjectByType<NetworkRunner>();

            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();

            if (_gameplay == null || _runner == null || !_runner.IsRunning)
                return;

            _gameplay.RPC_ToggleReady(_runner.LocalPlayer);
        }

        private void SetLobbyVisible(bool visible)
        {
            if (mainLobbyPanel != null)
                mainLobbyPanel.SetActive(visible);

            if (readyButton != null)
                readyButton.SetActive(visible);

            if (!visible && startGameButton != null)
                startGameButton.SetActive(false);
        }
    }
}
