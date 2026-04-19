using UnityEngine;
using UnityEngine.EventSystems;

namespace HackathonJuego
{
    /// <summary>
    /// Ponlo en cualquier botón UI para que suene hover y click automáticamente.
    /// </summary>
    public class UIAudioTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUIHover();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUIClick();
        }
    }
}
