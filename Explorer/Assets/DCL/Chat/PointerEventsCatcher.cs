using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace DCL.Chat
{
    public class PointerEventsCatcher : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public UnityEvent<PointerEventData> PointerEntered;
        public UnityEvent<PointerEventData> PointerExited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered?.Invoke(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExited?.Invoke(eventData);
        }
    }
}
