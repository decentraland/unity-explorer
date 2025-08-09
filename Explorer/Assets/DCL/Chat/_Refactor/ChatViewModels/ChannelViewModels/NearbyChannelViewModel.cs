using DCL.Chat.History;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class NearbyChannelViewModel : BaseChannelViewModel
    {
        public string DisplayName { get; }
        public Sprite Icon { get; }

        public NearbyChannelViewModel(ChatChannel.ChannelId id, string displayName, Sprite icon, int unreadMessagesCount, bool hasUnreadMentions)
            : base(id, ChatChannel.ChatChannelType.NEARBY, unreadMessagesCount, hasUnreadMentions)
        {
            DisplayName = displayName;
            Icon = icon;
        }
    }
}