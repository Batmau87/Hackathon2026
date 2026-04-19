using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HackathonJuego
{
    public class PlayerClicker : NetworkBehaviour
    {
        [Header("Configuración de Raycast")]
        public LayerMask interactableLayer;

        private Gameplay _gameplay;

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            if (_gameplay == null) _gameplay = FindFirstObjectByType<Gameplay>();
            
            // --- EL ARREGLO ESTÁ AQUÍ ---
            // Buscamos dinámicamente la cámara que está ACTIVA en este frame para este jugador local
            Camera camaraActiva = null;
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.isActiveAndEnabled)
                {
                    camaraActiva = cam;
                    break;
                }
            }

            if (camaraActiva == null)
            {
                Debug.LogWarning("No se encontró ninguna cámara activa para disparar el raycast.");
                return;
            }

            // Disparamos usando la cámara que realmente le pertenece a este jugador
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = camaraActiva.ScreenPointToRay(mousePosition);
            
            Debug.DrawRay(ray.origin, ray.direction * 100f, Color.green, 2f);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
            {
                GameObject objetoTocado = hit.collider.gameObject;
                
                // 1. LÓGICA DEL JUGADOR 0 (Configurador)
                if (objetoTocado.CompareTag("BotonConfig"))
                {
                    int opcion = 1;
                    if (objetoTocado.name == "Boton_Opcion2") opcion = 2;

                    if (_gameplay != null) _gameplay.RPC_SeleccionarPaquete(opcion);
                    Debug.Log($"Clickeé el botón {opcion}, enviando señal...");
                }
                // 2. LÓGICA DEL JUGADOR 1 (Informado)
                else if (objetoTocado.CompareTag("CajaJuego"))
                {
                    int boxIndex = 0;
                    if (objetoTocado.name == "Caja_1") boxIndex = 1;
                    else if (objetoTocado.name == "Caja_2") boxIndex = 2;

                    if (_gameplay != null) _gameplay.RPC_InspeccionarCaja(boxIndex);
                    Debug.Log($"Intentando inspeccionar la caja {boxIndex}...");
                }
            }
        }
    }
}