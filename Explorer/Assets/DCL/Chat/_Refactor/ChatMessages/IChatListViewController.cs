using System.Collections.Generic;
using DCL.Chat.ChatViewModels;

namespace DCL.Chat.ChatMessages
{
    public interface IChatListViewController
    {
        void SetItems(IReadOnlyList<ChatMessageViewModel> items);
        void AddItem(ChatMessageViewModel item);
        void UpdateItem(ChatMessageViewModel item);
        void Clear();
    }
}