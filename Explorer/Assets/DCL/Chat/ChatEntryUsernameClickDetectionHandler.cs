using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat
{
    public class ChatEntryUsernameClickDetectionHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action UserNameClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            UserNameClicked?.Invoke();
        }
    }
}
