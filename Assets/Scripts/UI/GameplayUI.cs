using UnityEngine;
using Fusion;

namespace HackathonJuego
{
    /// <summary>
    /// Controla qué panel se muestra según el rol del jugador local y el estado del juego.
    /// Ponlo en el Canvas de la escena Hackathon2026.
    /// </summary>
    public class GameplayUI : MonoBehaviour
    {
        [Header("Paneles de Acción (cuando es TU turno)")]
        public GameObject architectPanel;
        public GameObject observerPanel;
        public GameObject judgePanel;

        [Header("Paneles de Espera (cuando NO es tu turno)")]
        public GameObject waitArchitectPanel;
        public GameObject waitObserverPanel;
        public GameObject waitJudgePanel;

        [Header("Textos de Espera (opcional, dentro de cada panel)")]
        public TMPro.TextMeshProUGUI waitArchitectText;
        public TMPro.TextMeshProUGUI waitObserverText;
        public TMPro.TextMeshProUGUI waitJudgeText;

        [Header("Paneles de Resultado")]
        public GameObject victoriaPanel;
        public GameObject derrotaPanel;

        private Gameplay _gameplay;
        private NetworkRunner _runner;
        private EGameplayState _lastState = EGameplayState.Lobby;
        private int _localStation = -1;

        private GameObject[] AllPanels => new[]
        {
            architectPanel, observerPanel, judgePanel,
            waitArchitectPanel, waitObserverPanel, waitJudgePanel,
            victoriaPanel, derrotaPanel
        };

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (_runner == null) _runner = FindFirstObjectByType<NetworkRunner>();
            if (_gameplay == null) _gameplay = FindFirstObjectByType<Gameplay>();
            if (_runner == null || _gameplay == null || _gameplay.Object == null || !_gameplay.Object.IsValid) return;

            if (_gameplay.State == EGameplayState.Lobby) return;

            _localStation = _gameplay.GetLocalStationIndex();
            if (_localStation < 0)
            {
                Debug.Log($"[GameplayUI] LocalStation = -1, LocalPlayer = {_runner.LocalPlayer}, State = {_gameplay.State}");
                return;
            }

            if (_gameplay.State != _lastState)
            {
                _lastState = _gameplay.State;
                Debug.Log($"[GameplayUI] Estado cambió a {_gameplay.State}, mi estación = {_localStation}");
                RefreshUI();
            }

            // Timer del Juez
            if (_gameplay.State == EGameplayState.P2_Distribute && _localStation == 2)
            {
                var judgeScript = judgePanel != null ? judgePanel.GetComponent<JudgePanel>() : null;
                if (judgeScript != null)
                    judgeScript.UpdateTimer(_gameplay.JudgeTimer);
            }
        }

        private void RefreshUI()
        {
            // Apagar todos
            foreach (var p in AllPanels)
                if (p != null) p.SetActive(false);

            switch (_gameplay.State)
            {
                case EGameplayState.P0_Config:
                    if (_localStation == 0)
                        Show(architectPanel);
                    else
                        ShowWait("El Arquitecto está eligiendo el contenido de las cajas...");
                    break;

                case EGameplayState.P1_Inspect:
                    if (_localStation == 1)
                        Show(observerPanel);
                    else
                        ShowWait(_localStation == 0
                            ? "Las cajas han sido enviadas.\nEl Observador está inspeccionando..."
                            : "El Observador está viendo el contenido de 1 caja...");
                    break;

                case EGameplayState.P1_Pass:
                    if (_localStation == 1)
                    {
                        Show(observerPanel);
                        var obs = observerPanel != null ? observerPanel.GetComponent<ObserverPanel>() : null;
                        if (obs != null) obs.ShowPassPhase();
                    }
                    else
                        ShowWait("El Observador está pasando las cajas...");
                    break;

                case EGameplayState.P2_Distribute:
                    if (_localStation == 2)
                        Show(judgePanel);
                    else
                        ShowWait("El Juez está repartiendo las cajas...");
                    break;

                case EGameplayState.Reveal:
                case EGameplayState.Finished:
                    ShowResult();
                    break;
            }
        }

        private void Show(GameObject panel)
        {
            if (panel != null) panel.SetActive(true);
        }

        /// <summary>
        /// Muestra el panel de espera correspondiente al rol local.
        /// </summary>
        private void ShowWait(string message)
        {
            switch (_localStation)
            {
                case 0:
                    Show(waitArchitectPanel);
                    if (waitArchitectText != null) waitArchitectText.text = message;
                    break;
                case 1:
                    Show(waitObserverPanel);
                    if (waitObserverText != null) waitObserverText.text = message;
                    break;
                case 2:
                    Show(waitJudgePanel);
                    if (waitJudgeText != null) waitJudgeText.text = message;
                    break;
            }
        }

        private void ShowResult()
        {
            if (_gameplay == null) return;

            // Calcular si el jugador local ganó o perdió
            int myScore = 0;
            if (_gameplay.PlayerData.TryGet(_runner.LocalPlayer, out var myData))
                myScore = _gameplay.RoundScores[myData.StationIndex];

            if (myScore > 0)
                Show(victoriaPanel);
            else
                Show(derrotaPanel);
        }
    }
}
