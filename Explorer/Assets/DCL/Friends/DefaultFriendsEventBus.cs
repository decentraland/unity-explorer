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
        public event Action<string>? OnFriendRemoved;

        public void BroadcastFriendRequestReceived(FriendRequest request) =>
            OnFriendRequestReceived?.Invoke(request);

        public void BroadcastFriendRequestSent(FriendRequest request) =>
            OnFriendRequestSent?.Invoke(request);

        public void BroadcastFriendRequestAccepted(string friendRequestId) =>
            OnFriendRequestAccepted?.Invoke(friendRequestId);

        public void BroadcastFriendRequestRejected(string friendRequestId) =>
            OnFriendRequestRejected?.Invoke(friendRequestId);

        public void BroadcastFriendRequestCanceled(string friendRequestId) =>
            OnFriendRequestCanceled?.Invoke(friendRequestId);

        public void BroadcastFriendRemoved(string friendId) =>
            OnFriendRemoved?.Invoke(friendId);
    }
}
