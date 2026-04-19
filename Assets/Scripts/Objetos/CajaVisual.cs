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
            Debug.Log($"<color=magenta>¡Activando animación en la caja! Premio recibido: {tipoPremio}</color>");

            // 1. Apagamos ambos y prendemos el correcto
            if (objDinero != null) objDinero.SetActive(false);
            if (objBomba != null) objBomba.SetActive(false);

            if (tipoPremio == 1 && objDinero != null) objDinero.SetActive(true);
            else if (tipoPremio == 2 && objBomba != null) objBomba.SetActive(true);

            // 2. Disparamos los Triggers
            if (boxAnimator != null) 
            {
                boxAnimator.SetTrigger("Abrir");
                Debug.Log("Trigger 'Abrir' enviado al BoxAnimator.");
            }
            else 
            {
                Debug.LogError("El boxAnimator no está asignado en el Inspector de esta caja.");
            }

            if (premioAnimator != null) 
            {
                premioAnimator.SetTrigger("Subir");
                Debug.Log("Trigger 'Subir' enviado al PremioAnimator.");
            }
            else 
            {
                Debug.LogError("El premioAnimator no está asignado en el Inspector de esta caja.");
            }
        }
    }
}