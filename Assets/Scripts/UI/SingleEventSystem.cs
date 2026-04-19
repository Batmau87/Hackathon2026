using UnityEngine;
using UnityEngine.EventSystems;

namespace HackathonJuego
{
    /// <summary>
    /// Destruye este EventSystem si ya existe otro en la escena.
    /// Ponlo en cada EventSystem para evitar duplicados.
    /// </summary>
    public class SingleEventSystem : MonoBehaviour
    {
        private void Awake()
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (all.Length > 1)
            {
                Debug.Log($"[SingleEventSystem] EventSystem duplicado detectado, destruyendo: {gameObject.name}");
                Destroy(gameObject);
            }
        }
    }
}
