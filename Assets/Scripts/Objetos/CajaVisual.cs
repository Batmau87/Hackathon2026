using UnityEngine;

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

        // El servidor llamará a esta función SOLAMENTE en la compu del Jugador 1
        public void RevelarPremioExclusivo(int tipoPremio)
        {
            // 1. Apagamos ambos por si acaso y prendemos el correcto
            objDinero.SetActive(false);
            objBomba.SetActive(false);

            if (tipoPremio == 1) objDinero.SetActive(true);
            else if (tipoPremio == 2) objBomba.SetActive(true);

            // 2. Disparamos los Triggers de animación
            if (boxAnimator != null) boxAnimator.SetTrigger("Abrir");
            if (premioAnimator != null) premioAnimator.SetTrigger("Subir");
        }
    }
}