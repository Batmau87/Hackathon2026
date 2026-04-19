using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

namespace HackathonJuego
{
    /// <summary>
    /// Panel del Observador (Station 1).
    /// Fase Inspect: muestra 3 botones de caja para elegir cuál abrir.
    /// Fase Pass: muestra instrucción de swipe/arrastre para pasar las cajas.
    /// </summary>
    public class ObserverPanel : MonoBehaviour
    {
        [Header("Fase Inspeccionar")]
        public GameObject inspectPhase;
        public Button box0Button;
        public Button box1Button;
        public Button box2Button;
        public TextMeshProUGUI inspectInstructions;

        [Header("Fase Pasar")]
        public GameObject passPhase;
        public TextMeshProUGUI passInstructions;

        [Header("Swipe para pasar")]
        [Tooltip("Distancia mínima en píxeles para considerar un swipe")]
        public float swipeThreshold = 100f;

        private Gameplay _gameplay;
        private bool _boxSelected = false;
        private Vector2 _dragStart;
        private bool _isDragging = false;

        private void OnEnable()
        {
            // Mostrar fase de inspección por defecto
            SetPhase(inspect: true);
            _boxSelected = false;

            box0Button?.onClick.AddListener(() => OnBoxClicked(0));
            box1Button?.onClick.AddListener(() => OnBoxClicked(1));
            box2Button?.onClick.AddListener(() => OnBoxClicked(2));
        }

        private void OnDisable()
        {
            box0Button?.onClick.RemoveAllListeners();
            box1Button?.onClick.RemoveAllListeners();
            box2Button?.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (passPhase != null && passPhase.activeSelf)
                HandleSwipe();
        }

        private void SetPhase(bool inspect)
        {
            if (inspectPhase != null) inspectPhase.SetActive(inspect);
            if (passPhase != null) passPhase.SetActive(!inspect);
        }

        /// <summary>
        /// Llamado por GameplayUI cuando el estado cambia a P1_Pass
        /// </summary>
        public void ShowPassPhase()
        {
            SetPhase(inspect: false);
            if (passInstructions != null)
                passInstructions.text = "Arrastra hacia la derecha →\npara pasar las cajas al Juez";
        }

        private void OnBoxClicked(int boxIndex)
        {
            if (_boxSelected) return;

            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();
            if (_gameplay == null) return;

            _boxSelected = true;
            _gameplay.RPC_InspeccionarCaja(boxIndex, _gameplay.Runner.LocalPlayer);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBoxOpen();

            // Desactivar los otros botones visualmente
            if (box0Button != null) box0Button.interactable = (boxIndex == 0);
            if (box1Button != null) box1Button.interactable = (boxIndex == 1);
            if (box2Button != null) box2Button.interactable = (boxIndex == 2);

            if (inspectInstructions != null)
                inspectInstructions.text = $"¡Has abierto la caja {boxIndex + 1}!";
        }

        private void HandleSwipe()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _dragStart = Input.mousePosition;
                _isDragging = true;
            }

            if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                _isDragging = false;
                Vector2 dragEnd = Input.mousePosition;
                float deltaX = dragEnd.x - _dragStart.x;

                // Swipe a la derecha
                if (deltaX > swipeThreshold)
                {
                    ConfirmPass();
                }
            }
        }

        private void ConfirmPass()
        {
            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();
            if (_gameplay == null) return;

            _gameplay.RPC_PasarCajas(_gameplay.Runner.LocalPlayer);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBoxSlide();
        }
    }
}
