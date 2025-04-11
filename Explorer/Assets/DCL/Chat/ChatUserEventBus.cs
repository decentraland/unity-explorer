using System;
using UnityEngine;

namespace DCL.Chat
{
    public interface IChatUserStateEventBus
    {
        public delegate void UserDelegate(string userId);

        event UserDelegate? FriendConnected;
        event UserDelegate? UserDisconnected;
        event UserDelegate? NonFriendConnected;
        event UserDelegate? UserBlocked;
        event Action? CurrentConversationUserUnavailable;
        event Action? CurrentConversationUserAvailable;

        void OnFriendConnected(string userId);
        void OnUserDisconnected(string userId);
        void OnNonFriendConnected(string userId);
        void OnUserBlocked(string userId);
        void OnCurrentConversationUserAvailable();
        void OnCurrentConversationUserUnavailable();
    }

    public class ChatUserStateEventBus : IChatUserStateEventBus
    {
        public event IChatUserStateEventBus.UserDelegate? FriendConnected;
        public event IChatUserStateEventBus.UserDelegate? UserDisconnected;
        public event IChatUserStateEventBus.UserDelegate? NonFriendConnected;
        public event IChatUserStateEventBus.UserDelegate? UserBlocked;

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
    }
}
