using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HackathonJuego
{
    public class PlayerClicker : NetworkBehaviour
    {
        public LayerMask interactableLayer;
        private Gameplay _gameplay;

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            if (_gameplay == null) _gameplay = FindFirstObjectByType<Gameplay>();
            if (_gameplay == null) return;

            // --- BLOQUEO DE SEGURIDAD ---
            // Solo puedes hacer clic si es tu turno según el StationIndex
            if (_gameplay.PlayerData.TryGet(Runner.LocalPlayer, out var myData))
            {
                if (myData.StationIndex != _gameplay.PlayerTurnIndex) return;
            }

            Camera camaraActiva = null;
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam.isActiveAndEnabled) { camaraActiva = cam; break; }
            }

            if (camaraActiva == null) return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = camaraActiva.ScreenPointToRay(mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
            {
                GameObject objetoTocado = hit.collider.gameObject;
                
                if (objetoTocado.CompareTag("BotonConfig"))
                {
                    int opcion = objetoTocado.name == "Boton_Opcion2" ? 2 : 1;
                    _gameplay.RPC_SeleccionarPaquete(opcion);
                }
                else if (objetoTocado.CompareTag("CajaJuego"))
                {
                    // Sacamos el índice del nombre del objeto (Caja_0, Caja_1, Caja_2)
                    string name = objetoTocado.name;
                    if (int.TryParse(name.Substring(name.Length - 1), out int index))
                    {
                        // Si estamos en fase de inspección, el Jugador 1 inspecciona
                        if (_gameplay.State == EGameplayState.P1_Inspect)
                        {
                            _gameplay.RPC_InspeccionarCaja(index);
                        }
                        // Si estamos en fase de distribución, el Jugador 2 elige a quién darle la caja
                        // (Aquí podrías abrir un pequeño menú UI o arrastrar)
                    }
                }
            }
        }
    }
}