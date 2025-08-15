using System.Collections.Generic;
using DCL.Chat.ChatViewModels;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    public abstract class ChatListViewAdapterBase : MonoBehaviour
    {
        public abstract void SetItems(IReadOnlyList<ChatMessageViewModel> items);

        public abstract void AddItem(ChatMessageViewModel item);

        public abstract void UpdateItem(ChatMessageViewModel item);

        public abstract void Clear();
    }
}
