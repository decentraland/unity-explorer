using DCL.Chat.History;
using DCL.Utilities;
using DCL.Utility.Types;
using System;

namespace DCL.Chat.ChatServices
{
    public class CurrentChannelService : IDisposable
    {
        private static readonly ReactiveProperty<ChatChannel> DEFAULT_CHANNEL = new ReactiveProperty<ChatChannel>(ChatChannel.NEARBY_CHANNEL);

        public Result<PrivateConversationUserStateService.ChatUserState> InputState { get; internal set; }

        public ChatChannel CurrentChannel => currentChannel.Value;
        public IReadonlyReactiveProperty<ChatChannel> CurrentChannelProperty => currentChannel;

        private readonly ReactiveProperty<ChatChannel> currentChannel = DEFAULT_CHANNEL;

        public ChatChannel.ChannelId CurrentChannelId => currentChannel.Value?.Id ?? default;

        public ICurrentChannelUserStateService? UserStateService { get; private set; }

        public void SetCurrentChannel(ChatChannel newChannel, ICurrentChannelUserStateService userStateService)
        {
            if (currentChannel.Value == newChannel)
                return;

            currentChannel.UpdateValue(newChannel);
            UserStateService = userStateService;
        }

        public void Dispose()
        {
            currentChannel.ClearSubscriptionsList();
        }

        public void Reset()
        {
            currentChannel.UpdateValue(DEFAULT_CHANNEL);
            UserStateService?.Deactivate();
            UserStateService = null;
        }
    }
}


