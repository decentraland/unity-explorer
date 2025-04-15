using Cysharp.Threading.Tasks;
using DCL.UI;

namespace DCL.Chat
{
    public class ChatControllerUserStateHelper
    {
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly IChatController chatController;

        public ChatControllerUserStateHelper(
            ChatUserStateUpdater chatUserStateUpdater,
            IChatController chatController)
        {
            this.chatUserStateUpdater = chatUserStateUpdater;
            this.chatController = chatController;
        }

        public void OnUserDisconnected(string userId)
        {
            var state = chatUserStateUpdater.GetDisconnectedUserState(userId);
            chatController.SetInputWithUserState(state);
        }

        public void OnNonFriendConnected(string userId)
        {
            GetAndSetupNonFriendUserStateAsync(userId).Forget();
        }

        private async UniTaskVoid GetAndSetupNonFriendUserStateAsync(string userId)
        {
            //We might need a new state of type "LOADING" or similar to display until we resolve the real state
            chatController.SetInputWithUserState(ChatUserStateUpdater.ChatUserState.DISCONNECTED);
            var state = await chatUserStateUpdater.GetConnectedNonFriendUserStateAsync(userId);
            chatController.SetInputWithUserState(state);
        }

        public void OnFriendConnected(string _)
        {
            var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
            chatController.SetInputWithUserState(state);
        }

        public void OnUserBlockedByOwnUser()
        {
            var state = ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER;
            chatController.SetInputWithUserState(state);
        }

        public void OnCurrentConversationUserUnavailable()
        {
            var state = ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED;
            chatController.SetInputWithUserState(state);
        }

        public void OnCurrentConversationUserAvailable()
        {
            var state = ChatUserStateUpdater.ChatUserState.CONNECTED;
            chatController.SetInputWithUserState(state);
        }

        public void OnUserConnectionStateChanged(string userId, bool isConnected)
        {
            chatController.UpdateConversationToolbarStatusIcon(userId, isConnected ? OnlineStatus.ONLINE : OnlineStatus.OFFLINE);
        }
    }
}
