using System;

namespace DCL.Friends
{
    public interface IFriendsEventBus
    {
        public delegate void UserIdOperation(string userId);

        /// <summary>
        /// Other user sent you a friend request
        /// </summary>
        event Action<FriendRequest> OnFriendRequestReceived;
        event UserIdOperation OnOtherUserAcceptedYourRequest;
        event UserIdOperation OnOtherUserRejectedYourRequest;
        event UserIdOperation OnOtherUserCancelledTheRequest;
        event UserIdOperation OnOtherUserRemovedTheFriendship;

        event Action<FriendRequest> OnYouSentFriendRequestToOtherUser;
        event UserIdOperation OnYouRemovedFriend;
        event UserIdOperation OnYouCancelledFriendRequestSentToOtherUser;
        event UserIdOperation OnYouAcceptedFriendRequestReceivedFromOtherUser;
        event UserIdOperation OnYouRejectedFriendRequestReceivedFromOtherUser;

        event Action<FriendProfile> OnFriendConnected;
        event Action<FriendProfile> OnFriendDisconnected;
        event Action<FriendProfile> OnFriendAway;

        void BroadcastFriendRequestReceived(FriendRequest request);

        void BroadcastThatOtherUserAcceptedYourRequest(string userId);

        void BroadcastThatOtherUserRejectedYourRequest(string userId);

        void BroadcastThatOtherUserCancelledTheRequest(string userId);

        void BroadcastThatOtherUserRemovedTheFriendship(string userId);

        void BroadcastFriendConnected(FriendProfile friend);

        void BroadcastFriendDisconnected(FriendProfile friend);

        void BroadcastFriendAsAway(FriendProfile friend);

        void BroadcastThatYouSentFriendRequestToOtherUser(FriendRequest request);

        void BroadcastThatYouRemovedFriend(string userId);

        void BroadcastThatYouAcceptedFriendRequestReceivedFromOtherUser(string userId);

        void BroadcastThatYouCancelledFriendRequestSentToOtherUser(string userId);

        void BroadcastThatYouRejectedFriendRequestReceivedFromOtherUser(string userId);
    }
}
