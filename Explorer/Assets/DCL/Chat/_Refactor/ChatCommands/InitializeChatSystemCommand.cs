using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.Services;
using DCL.Friends;
using DCL.Utilities;

using Utility;

namespace DCL.Chat.ChatUseCases
{
    public class InitializeChatSystemCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly ICurrentChannelService currentChannelService;

        public InitializeChatSystemCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            ChatHistoryStorage? chatHistoryStorage,
            ChatUserStateUpdater chatUserStateUpdater,
            ICurrentChannelService currentChannelService)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.friendsServiceProxy = friendsServiceProxy;
            this.chatHistoryStorage = chatHistoryStorage;
            this.chatUserStateUpdater = chatUserStateUpdater;
            this.currentChannelService = currentChannelService;
        }

        public async UniTaskVoid ExecuteAsync(CancellationToken ct)
        {
            ChatChannel nearbyChannel = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);

            if (nearbyChannel.Messages.Count == 0)
                chatHistory.AddMessage(nearbyChannel.Id, ChatMessage.NewFromSystem("Type /help for available commands."));

            if (!friendsServiceProxy.Configured) return;

            chatHistoryStorage?.LoadAllChannelsWithoutMessages();

            ct.ThrowIfCancellationRequested();

            HashSet<string>? connectedUsers = await chatUserStateUpdater.InitializeAsync(chatHistory.Channels.Keys);
            eventBus.Publish(new ChatEvents.InitialUserStatusLoadedEvent { Users = connectedUsers });

            SetDefaultChannel(nearbyChannel);
        }

        private void SetDefaultChannel(ChatChannel nearbyChannel)
        {
            currentChannelService.SetCurrentChannel(nearbyChannel);
            eventBus.Publish(new ChatEvents.ChannelSelectedEvent { Channel = nearbyChannel });
        }
    }
}
