using System;

namespace DCL.Friends
{
    public class DefaultFriendsEventBus : IFriendsEventBus
    {
        public event Action<FriendRequest>? OnFriendRequestReceived;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserRemovedTheRequest;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserAcceptedYourRequest;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserRejectedYourRequest;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserCancelledTheRequest;
        public event Action<FriendRequest>? OnYouSentFriendRequestToOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouRemovedFriendRequestSentToOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouCancelledFriendRequestSentToOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouAcceptedFriendRequestReceivedFromOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouRejectedFriendRequestReceivedFromOtherUser;
        public event Action<FriendProfile>? OnFriendConnected;
        public event Action<FriendProfile>? OnFriendDisconnected;
        public event Action<FriendProfile>? OnFriendAway;

        public void BroadcastFriendRequestReceived(FriendRequest request) =>
            OnFriendRequestReceived?.Invoke(request);

        public void BroadcastThatYouSentFriendRequestToOtherUser(FriendRequest request) =>
            OnYouSentFriendRequestToOtherUser?.Invoke(request);

        public void BroadcastThatYouRemovedFriendRequestSentToOtherUser(string userId) =>
            OnYouRemovedFriendRequestSentToOtherUser?.Invoke(userId);

        public void BroadcastThatYouAcceptedFriendRequestReceivedFromOtherUser(string userId) =>
            OnYouAcceptedFriendRequestReceivedFromOtherUser?.Invoke(userId);

        public void BroadcastThatYouCancelledFriendRequestSentToOtherUser(string userId) =>
            OnYouCancelledFriendRequestSentToOtherUser?.Invoke(userId);

        public void BroadcastThatYouRejectedFriendRequestReceivedFromOtherUser(string userId) =>
            OnYouRejectedFriendRequestReceivedFromOtherUser?.Invoke(userId);

        public void BroadcastThatOtherUserAcceptedYourRequest(string userId) =>
            OnOtherUserAcceptedYourRequest?.Invoke(userId);

        public void BroadcastThatOtherUserRejectedYourRequest(string userId) =>
            OnOtherUserRejectedYourRequest?.Invoke(userId);

        public void BroadcastThatOtherUserCancelledTheRequest(string userId) =>
            OnOtherUserCancelledTheRequest?.Invoke(userId);

        public void BroadcastThatOtherUserRemovedTheRequest(string userId) =>
            OnOtherUserRemovedTheRequest?.Invoke(userId);

        public void BroadcastFriendConnected(FriendProfile friend) =>
            OnFriendConnected?.Invoke(friend);

        public void BroadcastFriendDisconnected(FriendProfile friend) =>
            OnFriendDisconnected?.Invoke(friend);

        public void BroadcastFriendAsAway(FriendProfile friend) =>
            OnFriendAway?.Invoke(friend);
    }
}
