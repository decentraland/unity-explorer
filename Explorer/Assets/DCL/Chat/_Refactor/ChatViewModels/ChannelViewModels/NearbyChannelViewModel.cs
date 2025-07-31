using DCL.Chat.History;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class NearbyChannelViewModel : BaseChannelViewModel
    {
        public string DisplayName { get; }
        public Sprite Icon { get; }

        public NearbyChannelViewModel(ChatChannel.ChannelId id, string displayName, Sprite icon)
            : base(id, ChatChannel.ChatChannelType.NEARBY)
        {
            DisplayName = displayName;
            Icon = icon;
        }
    }
}