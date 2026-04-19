using UnityEngine;
using DG.Tweening;

namespace HackathonJuego
{
    public class CajaVisual : MonoBehaviour
    {
        [Header("Animadores")]
        public Animator boxAnimator;      // El que abre la tapa
        public Animator premioAnimator;   // El que sube el Contenedor_Premios

        [Header("Objetos Internos")]
        public GameObject objDinero;
        public GameObject objBomba;

        [Header("Movimiento")]
        public float dropDuration = 1.2f;
        public Ease dropEase = Ease.OutBounce;

        /// <summary>Estado de la caja: si está abierta o cerrada.</summary>
        [HideInInspector] public bool isOpen = false;

        private Vector3 _startPosition;

        private void Awake()
        {
            _startPosition = transform.position;
        }

        /// <summary>
        /// El servidor llama esto SOLO en la compu del Jugador 1 para revelar contenido.
        /// </summary>
        public void RevelarPremioExclusivo(int tipoPremio)
        {
            Debug.Log($"<color=magenta>¡Activando animación en la caja! Premio: {tipoPremio}</color>");

            if (objDinero != null) objDinero.SetActive(false);
            if (objBomba != null) objBomba.SetActive(false);

            if (tipoPremio == 1 && objDinero != null) objDinero.SetActive(true);
            else if (tipoPremio == 2 && objBomba != null) objBomba.SetActive(true);

            AbrirCaja();
        }

        /// <summary>Abre la caja con Animator.</summary>
        public void AbrirCaja()
        {
            if (isOpen) return;
            isOpen = true;

            if (boxAnimator != null)
            {
                boxAnimator.speed = 1f;
                boxAnimator.SetTrigger("Abrir");
            }

            if (premioAnimator != null)
            {
                premioAnimator.speed = 1f;
                premioAnimator.SetTrigger("Subir");
            }
        }

        /// <summary>Cierra la caja reproduciendo la animación de abrir en reversa.</summary>
        public void CerrarCaja()
        {
            if (!isOpen) return;
            isOpen = false;

            if (boxAnimator != null)
            {
                var state = boxAnimator.GetCurrentAnimatorStateInfo(0);
                boxAnimator.Play(state.fullPathHash, 0, state.normalizedTime);
                boxAnimator.speed = -1f;
            }

            if (premioAnimator != null)
            {
                var state = premioAnimator.GetCurrentAnimatorStateInfo(0);
                premioAnimator.Play(state.fullPathHash, 0, state.normalizedTime);
                premioAnimator.speed = -1f;
            }
        }

        /// <summary>Vuelve a la posición original.</summary>
        public void ResetPosicion()
        {
            transform.position = _startPosition;
            isOpen = false;
        }
    }
}