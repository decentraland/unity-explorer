using DCL.Chat.ChatCommands;
using DCL.Chat.History;
using System;
using Utility.Types;

namespace DCL.Chat.ChatServices
{
    public class CurrentChannelService : IDisposable
    {
        public event Action<ChatChannel?>? OnChannelChanged;

        public Result<ChatUserStateService.ChatUserState> InputState { get; internal set; }

        public ChatChannel? CurrentChannel { get; private set; }
        public ChatChannel.ChannelId CurrentChannelId => CurrentChannel?.Id ?? default;

        public void SetCurrentChannel(ChatChannel newChannel)
        {
            if (CurrentChannel == newChannel)
                return;

            CurrentChannel = newChannel;

            OnChannelChanged?.Invoke(CurrentChannel);
        }

        public void Dispose()
        {
            CurrentChannel = null;
        }
    }
}


