using DCL.Chat.History;
using System;
using Utility.Types;

namespace DCL.Chat.ChatServices
{
    public class CurrentChannelService : IDisposable
    {
        public Result<PrivateConversationUserStateService.ChatUserState> InputState { get; internal set; }

        public ChatChannel? CurrentChannel { get; private set; }
        public ChatChannel.ChannelId CurrentChannelId => CurrentChannel?.Id ?? default;

        public ICurrentChannelUserStateService? UserStateService { get; private set; }

        public void SetCurrentChannel(ChatChannel newChannel, ICurrentChannelUserStateService userStateService)
        {
            if (CurrentChannel == newChannel)
                return;

            CurrentChannel = newChannel;
            UserStateService = userStateService;
        }

        public void Dispose()
        {
            CurrentChannel = null;
        }
    }
}


