using System;

namespace DCL.Chat
{
    public interface IChatUserStateEventBus
    {
        public delegate void UserDelegate(string userId);

        event UserDelegate? FriendConnected;
        event UserDelegate? FriendDisconnected;
        event UserDelegate? NonFriendConnected;
        event UserDelegate? NonFriendDisconnected;
        event UserDelegate? UserUnavailableToChat;
        event UserDelegate? UserAvailableToChat;
        event UserDelegate? UserBlocked;

        void OnFriendConnected(string userId);
        void OnFriendDisconnected(string userId);
        void OnNonFriendConnected(string userId);
        void OnNonFriendDisconnected(string userId);
        void OnUserAvailableToChat(string userId);
        void OnUserUnavailableToChat(string userId);
        void OnUserBlocked(string userId);
    }

    public class ChatUserStateEventBus : IChatUserStateEventBus
    {
        public event IChatUserStateEventBus.UserDelegate? FriendConnected;
        public event IChatUserStateEventBus.UserDelegate? FriendDisconnected;
        public event IChatUserStateEventBus.UserDelegate? NonFriendConnected;
        public event IChatUserStateEventBus.UserDelegate? NonFriendDisconnected;
        public event IChatUserStateEventBus.UserDelegate? UserUnavailableToChat;
        public event IChatUserStateEventBus.UserDelegate? UserAvailableToChat;
        public event IChatUserStateEventBus.UserDelegate? UserBlocked;

        public void OnFriendConnected(string userId)
        {
            FriendConnected?.Invoke(userId);
        }

        public void OnFriendDisconnected(string userId)
        {
            FriendDisconnected?.Invoke(userId);
        }

        public void OnNonFriendConnected(string userId)
        {
            NonFriendConnected?.Invoke(userId);
        }

        public void OnNonFriendDisconnected(string userId)
        {
            NonFriendDisconnected?.Invoke(userId);
        }

        public void OnUserAvailableToChat(string userId)
        {
            UserAvailableToChat?.Invoke(userId);
        }

        public void OnUserUnavailableToChat(string userId)
        {
            UserUnavailableToChat?.Invoke(userId);
        }

        public void OnUserBlocked(string userId)
        {
            UserBlocked?.Invoke(userId);
        }

    }
}
