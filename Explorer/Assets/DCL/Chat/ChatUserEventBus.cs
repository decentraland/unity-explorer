using DCL.Diagnostics;
using System;

namespace DCL.Chat
{
    public interface IChatUserStateEventBus
    {
        public delegate void UserDelegate(string userId);
        public delegate void UserConnectionStateDelegate(string userId, bool isConnected);

        event UserDelegate? FriendConnected;
        event UserDelegate? UserDisconnected;
        event UserDelegate? NonFriendConnected;
        event UserDelegate? UserBlocked;
        event UserConnectionStateDelegate UserConnectionStateChanged;
        event Action? CurrentConversationUserUnavailable;
        event Action? CurrentConversationUserAvailable;

        void OnFriendConnected(string userId);
        void OnUserDisconnected(string userId);
        void OnNonFriendConnected(string userId);
        void OnUserBlocked(string userId);
        void OnUserConnectionStateChanged(string userId, bool isConnected);
        void OnCurrentConversationUserAvailable();
        void OnCurrentConversationUserUnavailable();
    }

    public class ChatUserStateEventBus : IChatUserStateEventBus
    {
        public event IChatUserStateEventBus.UserDelegate? FriendConnected;
        public event IChatUserStateEventBus.UserDelegate? UserDisconnected;
        public event IChatUserStateEventBus.UserDelegate? NonFriendConnected;
        public event IChatUserStateEventBus.UserDelegate? UserBlocked;
        public event IChatUserStateEventBus.UserConnectionStateDelegate? UserConnectionStateChanged;

        public event Action? CurrentConversationUserUnavailable;
        public event Action? CurrentConversationUserAvailable;

        public void OnFriendConnected(string userId)
        {
            FriendConnected?.Invoke(userId);
        }

        public void OnUserDisconnected(string userId)
        {
            UserDisconnected?.Invoke(userId);
        }

        public void OnNonFriendConnected(string userId)
        {
            NonFriendConnected?.Invoke(userId);
        }

        public void OnUserBlocked(string userId)
        {
            UserBlocked?.Invoke(userId);
        }

        public void OnCurrentConversationUserAvailable()
        {
            CurrentConversationUserAvailable?.Invoke();
        }

        public void OnCurrentConversationUserUnavailable()
        {
            CurrentConversationUserUnavailable?.Invoke();
        }

        public void OnUserConnectionStateChanged(string userId, bool isConnected)
        {
            ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"On User Connection State Changed {isConnected}");
            UserConnectionStateChanged?.Invoke(userId, isConnected);
        }
    }
}
