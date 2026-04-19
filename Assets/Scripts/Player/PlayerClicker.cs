using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HackathonJuego
{
    public class PlayerClicker : NetworkBehaviour
    {
        public LayerMask interactableLayer;

        private Gameplay _gameplay;

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (_gameplay == null)
                _gameplay = FindFirstObjectByType<Gameplay>();

            if (_gameplay == null || Runner == null || !Runner.IsRunning)
                return;

            if (_gameplay.State == EGameplayState.Lobby)
                return;

            // Evita disparar raycasts al mundo cuando el usuario esta interactuando con UI.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!_gameplay.PlayerData.TryGet(Runner.LocalPlayer, out var myData))
                return;

            if (myData.StationIndex != _gameplay.PlayerTurnIndex)
                return;

            Camera activeCamera = GetLocalGameplayCamera(myData.StationIndex);
            if (activeCamera == null)
                return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = activeCamera.ScreenPointToRay(mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
                return;

            GameObject interactableObject = ResolveInteractableObject(hit.collider);
            if (interactableObject == null)
                return;

            if (interactableObject.CompareTag("BotonConfig"))
            {
                int option = interactableObject.name.Contains("Opcion2") ? 2 : 1;
                _gameplay.RPC_SeleccionarPaquete(option);
                return;
            }

            if (!interactableObject.CompareTag("CajaJuego"))
                return;

            if (!TryGetBoxIndex(interactableObject.transform, out int boxIndex))
                return;

            // Fase P1_Inspect: Observer clickea caja para abrirla
            if (_gameplay.State == EGameplayState.P1_Inspect)
            {
                _gameplay.RPC_InspeccionarCaja(boxIndex);
                return;
            }

            // Fase P1_Pass: Observer clickea la caja abierta para cerrarla y pasarla
            if (_gameplay.State == EGameplayState.P1_Pass)
            {
                _gameplay.RPC_CerrarYPasarCajas();
                return;
            }
        }

        private Camera GetLocalGameplayCamera(int stationIndex)
        {
            if (_gameplay != null &&
                _gameplay.StationCameras != null &&
                stationIndex >= 0 &&
                stationIndex < _gameplay.StationCameras.Length)
            {
                Camera stationCamera = _gameplay.StationCameras[stationIndex];
                if (stationCamera != null)
                    return stationCamera;
            }

            foreach (Camera cameraCandidate in Camera.allCameras)
            {
                if (cameraCandidate != null && cameraCandidate.isActiveAndEnabled)
                    return cameraCandidate;
            }

            return null;
        }

        private static GameObject ResolveInteractableObject(Collider hitCollider)
        {
            Transform current = hitCollider != null ? hitCollider.transform : null;

            while (current != null)
            {
                if (current.CompareTag("CajaJuego") || current.CompareTag("BotonConfig"))
                    return current.gameObject;

                current = current.parent;
            }

            return hitCollider != null ? hitCollider.gameObject : null;
        }

        private static bool TryGetBoxIndex(Transform clickedTransform, out int boxIndex)
        {
            Transform current = clickedTransform;

            while (current != null)
            {
                string objectName = current.name;
                int underscoreIndex = objectName.LastIndexOf('_');

                if (underscoreIndex >= 0 &&
                    underscoreIndex < objectName.Length - 1 &&
                    int.TryParse(objectName.Substring(underscoreIndex + 1), out boxIndex))
                {
                    return true;
                }

                current = current.parent;
            }

            boxIndex = -1;
            return false;
        }
    }
}
