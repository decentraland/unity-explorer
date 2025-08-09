using DCL.Diagnostics;
using System;

namespace DCL.Chat
{
    [Obsolete]
    public interface IChatUserStateEventBus
    {
        public delegate void UserStateChangedDelegate(string userId);
        public delegate void UserConnectionStateChangedDelegate(string userId, bool isConnected);

        event UserStateChangedDelegate? FriendConnected;
        event UserStateChangedDelegate? UserDisconnected;
        event UserStateChangedDelegate? NonFriendConnected;
        event UserStateChangedDelegate? UserBlocked;
        event UserConnectionStateChangedDelegate UserConnectionStateChanged;
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

    [Obsolete]
    public class ChatUserStateEventBus : IChatUserStateEventBus
    {
        public event IChatUserStateEventBus.UserStateChangedDelegate? FriendConnected;
        public event IChatUserStateEventBus.UserStateChangedDelegate? UserDisconnected;
        public event IChatUserStateEventBus.UserStateChangedDelegate? NonFriendConnected;
        public event IChatUserStateEventBus.UserStateChangedDelegate? UserBlocked;
        public event IChatUserStateEventBus.UserConnectionStateChangedDelegate? UserConnectionStateChanged;

        public event Action? CurrentConversationUserUnavailable;
        public event Action? CurrentConversationUserAvailable;

        public void OnFriendConnected(string userId)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"On Friend Connected {userId}");
            FriendConnected?.Invoke(userId);
        }

        public void OnUserDisconnected(string userId)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"On User Disconnected {userId}");
            UserDisconnected?.Invoke(userId);
        }

        public void OnNonFriendConnected(string userId)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"On Non Friend Connected {userId}");
            NonFriendConnected?.Invoke(userId);
        }

        public void OnUserBlocked(string userId)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"On User Blocked {userId}");
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
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"On User Connection State Changed {isConnected}");
            UserConnectionStateChanged?.Invoke(userId, isConnected);
        }
    }
}
