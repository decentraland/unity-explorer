using System;

namespace DCL.Friends
{
    public interface IFriendsEventBus
    {
        event Action<FriendRequest> OnFriendRequestReceived;
        event Action<FriendRequest> OnFriendRequestSent;
        event Action<string> OnFriendRequestAccepted;
        event Action<string> OnFriendRequestRejected;
        event Action<string> OnFriendRequestCanceled;
        event Action<string> OnFriendRequestRemoved;
        event Action<string> OnFriendConnected;
        event Action<string> OnFriendDisconnected;

        void BroadcastFriendRequestReceived(FriendRequest request);

        void BroadcastFriendRequestSent(FriendRequest request);

        void BroadcastFriendRequestAccepted(string friendId);

        void BroadcastFriendRequestRejected(string friendId);

        void BroadcastFriendRequestCanceled(string friendId);

        void BroadcastFriendRequestRemoved(string friendId);

        void BroadcastFriendConnected(string friendId);

        void BroadcastFriendDisconnected(string friendId);
    }
}
