using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatMessageFeedView : MonoBehaviour, IChatMessageFeedView
    {
        public void SetMessages(IReadOnlyList<MessageData> messages)
        {
        }

        public void Clear()
        {
        }
        
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}