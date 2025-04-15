using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using System;
using System.Threading;
using DCL.UI;

namespace DCL.Chat
{
    public class ChatControllerConversationEventsHelper
    {
        private readonly IChatHistory chatHistory;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly Func<ChatView?> getViewInstance;
        private readonly Func<CancellationTokenSource> getChatUsersUpdateCts;
        private readonly Action<CancellationTokenSource> setChatUsersUpdateCts;

        public ChatControllerConversationEventsHelper(
            IChatHistory chatHistory,
            ChatUserStateUpdater chatUserStateUpdater,
            Func<ChatView?> getViewInstance,
            Func<CancellationTokenSource> getChatUsersUpdateCts,
            Action<CancellationTokenSource> setChatUsersUpdateCts)
        {
            this.chatHistory = chatHistory;
            this.chatUserStateUpdater = chatUserStateUpdater;
            this.getViewInstance = getViewInstance;
            this.getChatUsersUpdateCts = getChatUsersUpdateCts;
            this.setChatUsersUpdateCts = setChatUsersUpdateCts;
        }

        public void OnOpenConversation(string userId)
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.USER);
            chatUserStateUpdater.CurrentConversation = userId;
            chatUserStateUpdater.AddConversation(userId);
            getViewInstance()!.CurrentChannelId = channel.Id;
            
            var cts = getChatUsersUpdateCts().SafeRestart();
            setChatUsersUpdateCts(cts);
            UpdateChatUserStateAsync(userId, cts.Token, true).Forget();
        }

        public void OnSelectConversation(ChatChannel.ChannelId channelId)
        {
            chatUserStateUpdater.CurrentConversation = channelId.Id;
            getViewInstance()!.CurrentChannelId = channelId;

            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
            {
                getViewInstance()!.SetInputWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
                return;
            }

            chatUserStateUpdater.AddConversation(channelId.Id);
            var cts = getChatUsersUpdateCts().SafeRestart();
            setChatUsersUpdateCts(cts);
            UpdateChatUserStateAsync(channelId.Id, cts.Token).Forget();
        }

        private async UniTaskVoid UpdateChatUserStateAsync(string userId, CancellationToken ct, bool updateToolbar = false)
        {
            var userState = await chatUserStateUpdater.GetChatUserStateAsync(userId, ct);
            getViewInstance()!.SetInputWithUserState(userState);

            if (!updateToolbar) return;

            bool offline = userState == ChatUserStateUpdater.ChatUserState.DISCONNECTED
                           || userState == ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;

            getViewInstance()!.UpdateConversationToolbarStatusIconForUser(userId, offline ? OnlineStatus.OFFLINE : OnlineStatus.ONLINE);
        }
    }
} 