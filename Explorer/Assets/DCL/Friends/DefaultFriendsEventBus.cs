using System;

namespace DCL.Friends
{
    public class DefaultFriendsEventBus : IFriendsEventBus
    {
        public event Action<FriendRequest>? OnFriendRequestReceived;
        public event Action<FriendRequest>? OnFriendRequestSent;
        public event Action<string>? OnFriendRequestAccepted;
        public event Action<string>? OnFriendRequestRejected;
        public event Action<string>? OnFriendRequestCanceled;
        public event Action<string>? OnFriendRequestRemoved;
        public event Action<string>? OnFriendConnected;
        public event Action<string>? OnFriendDisconnected;

        public void BroadcastFriendRequestReceived(FriendRequest request) =>
            OnFriendRequestReceived?.Invoke(request);

        public void BroadcastFriendRequestSent(FriendRequest request) =>
            OnFriendRequestSent?.Invoke(request);

        public void BroadcastFriendRequestAccepted(string friendId) =>
            OnFriendRequestAccepted?.Invoke(friendId);

        public void BroadcastFriendRequestRejected(string friendId) =>
            OnFriendRequestRejected?.Invoke(friendId);

        public void BroadcastFriendRequestCanceled(string friendId) =>
            OnFriendRequestCanceled?.Invoke(friendId);

        public void BroadcastFriendRequestRemoved(string friendId) =>
            OnFriendRequestRemoved?.Invoke(friendId);

        public void BroadcastFriendConnected(string friendId) =>
            OnFriendConnected?.Invoke(friendId);

        public void BroadcastFriendDisconnected(string friendId) =>
            OnFriendDisconnected?.Invoke(friendId);
    }
}
