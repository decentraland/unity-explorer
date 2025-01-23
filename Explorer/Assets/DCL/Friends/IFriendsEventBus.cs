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
        event UserIdOperation OnOtherUserRemovedTheRequest;

        event Action<FriendRequest> OnYouSentFriendRequestToOtherUser;

        event Action<FriendProfile> OnFriendConnected;
        event Action<FriendProfile> OnFriendDisconnected;
        event Action<FriendProfile> OnFriendAway;

        void BroadcastFriendRequestReceived(FriendRequest request);

        void BroadcastThatOtherUserAcceptedYourRequest(string userId);

        void BroadcastThatOtherUserRejectedYourRequest(string userId);

        void BroadcastThatOtherUserCancelledTheRequest(string userId);

        void BroadcastThatOtherUserRemovedTheRequest(string userId);

        void BroadcastFriendConnected(FriendProfile friend);

        void BroadcastFriendDisconnected(FriendProfile friend);

        void BroadcastFriendAsAway(FriendProfile friend);

        void BroadcastThatYouSentFriendRequestToOtherUser(FriendRequest request);
    }
}
