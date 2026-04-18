using Fusion;
using UnityEngine;
using TMPro;

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
        private bool _lobbyApagado = false;

        private void Start()
        {
            if (mainLobbyPanel != null) mainLobbyPanel.SetActive(true);
            if (startGameButton != null) startGameButton.SetActive(false);
        }

        private void Update()
        {
            // FindFirstObjectByType es perfecto para Unity 6
            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();
            if (_gameplay == null) _gameplay = FindFirstObjectByType<Gameplay>();

            if (_runner == null || _gameplay == null || _gameplay.Object == null || !_gameplay.Object.IsValid) return;

            // 1. SI EL JUEGO YA EMPEZÓ (Apagar Lobby y preparar controles de mouse)
            if (_gameplay.State == EGameplayState.Running)
            {
                if (!_lobbyApagado)
                {
                    if (mainLobbyPanel != null) mainLobbyPanel.SetActive(false);
                    
                    // Aseguramos que el cursor esté visible para que puedan interactuar con las cajas
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    
                    _lobbyApagado = true;
                }
                return;
            }

            // 2. ACTUALIZACIÓN DE TEXTOS Y BOTONES (Fase de Lobby)
            if (_runner.IsRunning && _runner.SessionInfo != null && _runner.SessionInfo.IsValid)
            {
                if (sessionCodeText != null) sessionCodeText.text = $"CÓDIGO: {_runner.SessionInfo.Name}";

                int conectados = 0;
                bool todosEstanListos = true;

                foreach (var kvp in _gameplay.PlayerData)
                {
                    if (kvp.Value.IsConnected)
                    {
                        conectados++;
                        if (!kvp.Value.IsReady) todosEstanListos = false;
                    }
                }

                if (playerCountText != null) playerCountText.text = $"Jugadores: {conectados} / 3";

                if (statusText != null && _gameplay.PlayerData.TryGet(_runner.LocalPlayer, out var myData))
                {
                    string estadoReady = myData.IsReady ? "<color=green>LISTO</color>" : "<color=yellow>ESPERANDO...</color>";
                    statusText.text = $"Estado: {estadoReady}";
                }

                if (readyButton != null) readyButton.SetActive(true);

                if (startGameButton != null)
                {
                    bool isHost = _runner.IsServer;
                    // Para pruebas puedes bajar este número, pero idealmente deben ser 3
                    bool haySuficientes = conectados >= 1; 
                    startGameButton.SetActive(isHost && haySuficientes && todosEstanListos);
                }
            }
        }

        public void Btn_StartGame()
        {
            if (_gameplay != null) _gameplay.StartMatchFromUI();
        }

        public void Btn_ToggleReady()
        {
            if (_gameplay != null && _runner != null) _gameplay.RPC_ToggleReady(_runner.LocalPlayer);
        }
    }
}