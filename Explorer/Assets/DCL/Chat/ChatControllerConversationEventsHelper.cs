using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using System.Threading;
using DCL.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatControllerConversationEventsHelper
    {
        private readonly IChatHistory chatHistory;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly IChatController chatController;
        private CancellationTokenSource chatUsersUpdateCts = new();

        public ChatControllerConversationEventsHelper(
            IChatHistory chatHistory,
            ChatUserStateUpdater chatUserStateUpdater,
            IChatController chatController)
        {
            this.chatHistory = chatHistory;
            this.chatUserStateUpdater = chatUserStateUpdater;
            this.chatController = chatController;
        }

        public void OnOpenConversation(string userId)
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.USER);
            chatUserStateUpdater.CurrentConversation = userId;
            chatUserStateUpdater.AddConversation(userId);
            if (chatController.TryGetView(out var view))
            {
                view.CurrentChannelId = channel.Id;
            }

            chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
            UpdateChatUserStateAsync(userId, true, chatUsersUpdateCts.Token).Forget();
        }

        public void OnSelectConversation(ChatChannel.ChannelId channelId)
        {
            chatUserStateUpdater.CurrentConversation = channelId.Id;
            viewInstance!.CurrentChannelId = channelId;

            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
            {
                viewInstance!.SetInputWithUserState(ChatUserStateUpdater.ChatUserState.CONNECTED);
            }
            else
            {
                chatUserStateUpdater.AddConversation(channelId.Id);
                chatUsersUpdateCts = chatUsersUpdateCts.SafeRestart();
                UpdateChatUserStateAsync(channelId.Id, chatUsersUpdateCts.Token).Forget();
            }
        }

        private async UniTaskVoid UpdateChatUserStateAsync(string userId, bool updateToolbar, CancellationToken ct)
        {
            var userState = await chatUserStateUpdater.GetChatUserStateAsync(userId, ct);
            if (chatController.TryGetView(out var view))
            {
                view.SetInputWithUserState(userState);

                if (!updateToolbar) return;

                bool offline = userState == ChatUserStateUpdater.ChatUserState.DISCONNECTED
                             || userState == ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;

                view.UpdateConversationToolbarStatusIconForUser(userId, offline ? OnlineStatus.OFFLINE : OnlineStatus.ONLINE);
            }
        }

        public void Dispose()
        {
            chatUsersUpdateCts.SafeCancelAndDispose();
        }
    }
}
