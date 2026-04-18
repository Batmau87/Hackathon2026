using Fusion;
using UnityEngine;
using UnityEngine.InputSystem; 

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
            // Verificamos que haya un mouse conectado para evitar crasheos
            if (Mouse.current == null) return;

            // NUEVO SISTEMA: Solo disparamos cuando se presiona el clic izquierdo
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            if (_gameplay == null) _gameplay = FindFirstObjectByType<Gameplay>();
            
            // Buscar la cámara activa
            if (_myCamera == null) _myCamera = Camera.main; 
            if (_myCamera == null)
            {
                Debug.LogWarning("No se encontró Camera.main. Asegúrate de que la cámara activa tenga el tag 'MainCamera'.");
                return;
            }

            // NUEVO SISTEMA: Obtenemos la posición del mouse en la pantalla
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = _myCamera.ScreenPointToRay(mousePosition);
            
            // Dibuja el láser en la ventana 'SCENE'
            Debug.DrawRay(ray.origin, ray.direction * 100f, Color.green, 2f);

            // Disparamos el Raycast filtrando SOLO por nuestra LayerMask
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
            {
                Debug.Log($"¡Le dimos a algo! Objeto tocado: {hit.collider.gameObject.name}");
            }
            else
            {
                Debug.Log("El clic no tocó nada en la capa Interactable.");
            }
        }
    }
}