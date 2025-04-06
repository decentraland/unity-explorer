using DCL.Web3;

namespace DCL.Chat
{
    public interface IChatUserEventBus
    {
        public delegate void UserDelegate(string userAddress);

        event UserDelegate? FriendConnected;
        event UserDelegate? FriendDisconnected;
        event UserDelegate? NonFriendConnected;
        event UserDelegate? NonFriendDisconnected;
        event UserDelegate? UserUnavailableToChat;
        event UserDelegate? UserAvailableToChat;

        void OnFriendConnected(string userAddress);
        void OnFriendDisconnected(string userAddress);
        void OnNonFriendConnected(string userAddress);
        void OnNonFriendDisconnected(string userAddress);
        void OnUserAvailableToChat(string userAddress);
        void OnUserUnavailableToChat(string userAddress);
    }

    public class ChatUserEventBus : IChatUserEventBus
    {
        public event IChatUserEventBus.UserDelegate? FriendConnected;
        public event IChatUserEventBus.UserDelegate? FriendDisconnected;
        public event IChatUserEventBus.UserDelegate? NonFriendConnected;
        public event IChatUserEventBus.UserDelegate? NonFriendDisconnected;
        public event IChatUserEventBus.UserDelegate? UserUnavailableToChat;
        public event IChatUserEventBus.UserDelegate? UserAvailableToChat;

        public virtual void OnFriendConnected(string userAddress)
        {
            FriendConnected?.Invoke(userAddress);
        }

        public virtual void OnFriendDisconnected(string userAddress)
        {
            FriendDisconnected?.Invoke(userAddress);
        }

        public virtual void OnNonFriendConnected(string userAddress)
        {
            NonFriendConnected?.Invoke(userAddress);
        }

        public virtual void OnNonFriendDisconnected(string userAddress)
        {
            NonFriendDisconnected?.Invoke(userAddress);
        }

        public virtual void OnUserAvailableToChat(string userAddress)
        {
            UserAvailableToChat?.Invoke(userAddress);
        }

        public virtual void OnUserUnavailableToChat(string userAddress)
        {
            UserUnavailableToChat?.Invoke(userAddress);
        }
    }
}
