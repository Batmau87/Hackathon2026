using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Fusion;

namespace HackathonJuego
{
    /// <summary>
    /// Panel del Juez (Station 2).
    /// Muestra 3 cajas arrastrables y 3 zonas de drop (Arquitecto, Observador, Para mí).
    /// Si el timer se acaba, el servidor asigna aleatoriamente.
    /// </summary>
    public class JudgePanel : MonoBehaviour
    {
        [Header("Timer")]
        public TextMeshProUGUI timerText;

        [Header("Cajas Arrastrables")]
        public JudgeDraggableBox[] draggableBoxes = new JudgeDraggableBox[3];

        [Header("Zonas de Drop")]
        public RectTransform dropZoneArchitect;  // Izquierda
        public RectTransform dropZoneObserver;   // Derecha
        public RectTransform dropZoneJudge;      // Abajo

        [Header("Labels de las zonas")]
        public TextMeshProUGUI labelArchitect;
        public TextMeshProUGUI labelObserver;
        public TextMeshProUGUI labelJudge;

        private Gameplay _gameplay;
        private int _assignedCount = 0;

        private void OnEnable()
        {
            _assignedCount = 0;

            if (labelArchitect != null) labelArchitect.text = "← Arquitecto";
            if (labelObserver != null) labelObserver.text = "Observador →";
            if (labelJudge != null) labelJudge.text = "↓ Para mí";

            for (int i = 0; i < draggableBoxes.Length; i++)
            {
                if (draggableBoxes[i] != null)
                {
                    draggableBoxes[i].BoxIndex = i;
                    draggableBoxes[i].Panel = this;
                }
            }
        }

        public void UpdateTimer(float timeLeft)
        {
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
                timerText.text = $"{seconds}s";
                timerText.color = seconds <= 5 ? Color.red : Color.white;
            }
        }

        public void OnBoxDropped(int boxIndex, Vector2 screenPos)
        {
            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();
            if (_gameplay == null) return;

            int targetStation = GetDropZone(screenPos);
            if (targetStation < 0) return;

            _gameplay.RPC_AsignarCaja(boxIndex, targetStation);
            _assignedCount++;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBoxDrop();
        }

        private int GetDropZone(Vector2 screenPos)
        {
            if (IsInsideRect(screenPos, dropZoneArchitect)) return 0;
            if (IsInsideRect(screenPos, dropZoneObserver)) return 1;
            if (IsInsideRect(screenPos, dropZoneJudge)) return 2;
            return -1;
        }

        private bool IsInsideRect(Vector2 screenPos, RectTransform rect)
        {
            if (rect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos);
        }
    }

    /// <summary>
    /// Componente en cada caja arrastrable del panel del Juez.
    /// Ponlo en un UI Image que represente la caja.
    /// </summary>
    public class JudgeDraggableBox : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [HideInInspector] public int BoxIndex;
        [HideInInspector] public JudgePanel Panel;

        private RectTransform _rect;
        private Canvas _canvas;
        private Vector2 _originalPos;
        private bool _assigned = false;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            _assigned = false;
            if (_rect != null)
                _originalPos = _rect.anchoredPosition;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_assigned) return;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_assigned || _rect == null) return;

            float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            _rect.anchoredPosition += eventData.delta / scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_assigned) return;

            if (Panel != null)
            {
                Panel.OnBoxDropped(BoxIndex, eventData.position);
            }

            // Si no se asignó, volver a la posición original
            // (en caso de drop inválido)
            if (!_assigned && _rect != null)
            {
                // Verificar si fue asignada exitosamente consultando el gameplay
                var gameplay = FindFirstObjectByType<Gameplay>();
                if (gameplay != null && gameplay.BoxAssignments[BoxIndex] != -1)
                {
                    _assigned = true;
                    GetComponent<CanvasGroup>()?.SetAlpha(0.4f);
                }
                else
                {
                    _rect.anchoredPosition = _originalPos;
                }
            }
        }
    }

    // Extensión para CanvasGroup
    public static class CanvasGroupExtensions
    {
        public static void SetAlpha(this CanvasGroup cg, float alpha)
        {
            if (cg != null)
            {
                cg.alpha = alpha;
                cg.interactable = alpha > 0.5f;
            }
        }
    }
}
