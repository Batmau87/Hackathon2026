using Fusion;
using UnityEngine;

namespace HackathonJuego
{
    public class PlayerClicker : NetworkBehaviour
    {
        [Header("Configuración de Raycast")]
        [Tooltip("Selecciona aquí la capa 'Interactable' o la que usaste para tus modelos.")]
        public LayerMask interactableLayer;

        private Camera _myCamera;
        private Gameplay _gameplay;

        private void Update()
        {
            // Solo disparamos cuando se presiona el clic izquierdo
            if (!Input.GetMouseButtonDown(0)) return;

            if (_gameplay == null) _gameplay = FindFirstObjectByType<Gameplay>();
            
            // Buscar la cámara activa
            if (_myCamera == null) _myCamera = Camera.main; 
            if (_myCamera == null)
            {
                Debug.LogWarning("No se encontró Camera.main. Asegúrate de que la cámara activa tenga el tag 'MainCamera'.");
                return;
            }

            // Creamos el rayo desde la posición del mouse en la pantalla
            Ray ray = _myCamera.ScreenPointToRay(Input.mousePosition);
            
            // ESTO DIBUJA EL LÁSER EN LA VENTANA 'SCENE' (Muy útil para ver qué falla)
            Debug.DrawRay(ray.origin, ray.direction * 100f, Color.green, 2f);

            // Disparamos el Raycast filtrando SOLO por nuestra LayerMask
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
            {
                Debug.Log($"¡Le dimos a algo! Objeto tocado: {hit.collider.gameObject.name}");
                
                // Más adelante aquí pondremos la lógica para saber si es Caja o Botón
            }
            else
            {
                Debug.Log("El clic no tocó nada en la capa Interactable.");
            }
        }
    }
}