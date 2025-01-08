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
        event Action<string> OnFriendRemoved;

        void BroadcastFriendRequestReceived(FriendRequest request);

        void BroadcastFriendRequestSent(FriendRequest request);

        void BroadcastFriendRequestAccepted(string friendRequestId);

        void BroadcastFriendRequestRejected(string friendRequestId);

        void BroadcastFriendRequestCanceled(string friendRequestId);

        void BroadcastFriendRemoved(string friendId);
    }
}
