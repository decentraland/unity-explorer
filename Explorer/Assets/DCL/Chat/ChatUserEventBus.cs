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

        void OnFriendConnected(string userId);
        void OnFriendDisconnected(string userId);
        void OnNonFriendConnected(string userId);
        void OnNonFriendDisconnected(string userId);
        void OnUserAvailableToChat(string userId);
        void OnUserUnavailableToChat(string userId);
    }

    public class ChatUserStateEventBus : IChatUserStateEventBus
    {
        public event IChatUserStateEventBus.UserDelegate? FriendConnected;
        public event IChatUserStateEventBus.UserDelegate? FriendDisconnected;
        public event IChatUserStateEventBus.UserDelegate? NonFriendConnected;
        public event IChatUserStateEventBus.UserDelegate? NonFriendDisconnected;
        public event IChatUserStateEventBus.UserDelegate? UserUnavailableToChat;
        public event IChatUserStateEventBus.UserDelegate? UserAvailableToChat;

        public virtual void OnFriendConnected(string userId)
        {
            FriendConnected?.Invoke(userId);
        }

        public virtual void OnFriendDisconnected(string userId)
        {
            FriendDisconnected?.Invoke(userId);
        }

        public virtual void OnNonFriendConnected(string userId)
        {
            NonFriendConnected?.Invoke(userId);
        }

        public virtual void OnNonFriendDisconnected(string userId)
        {
            NonFriendDisconnected?.Invoke(userId);
        }

        public virtual void OnUserAvailableToChat(string userId)
        {
            UserAvailableToChat?.Invoke(userId);
        }

        public virtual void OnUserUnavailableToChat(string userId)
        {
            UserUnavailableToChat?.Invoke(userId);
        }
    }
}
