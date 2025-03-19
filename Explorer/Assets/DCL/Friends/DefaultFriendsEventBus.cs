using System;

namespace DCL.Friends
{
    public class DefaultFriendsEventBus : IFriendsEventBus
    {
        public event Action<FriendRequest>? OnFriendRequestReceived;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserRemovedTheFriendship;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserAcceptedYourRequest;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserRejectedYourRequest;
        public event IFriendsEventBus.UserIdOperation? OnOtherUserCancelledTheRequest;
        public event Action<FriendRequest>? OnYouSentFriendRequestToOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouRemovedFriend;
        public event IFriendsEventBus.UserIdOperation? OnYouCancelledFriendRequestSentToOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouAcceptedFriendRequestReceivedFromOtherUser;
        public event IFriendsEventBus.UserIdOperation? OnYouRejectedFriendRequestReceivedFromOtherUser;
        public event Action<FriendProfile>? OnFriendConnected;
        public event Action<FriendProfile>? OnFriendDisconnected;
        public event Action<FriendProfile>? OnFriendAway;

        public event Action<BlockedProfile>? OnYouBlockedProfile;
        public event Action<BlockedProfile>? OnYouUnblockedProfile;

        public event Action<string>? OnYouBlockedByUser;
        public event Action<string>? OnYouUnblockedByUser;

        public void BroadcastFriendRequestReceived(FriendRequest request) =>
            OnFriendRequestReceived?.Invoke(request);

        public void BroadcastThatYouSentFriendRequestToOtherUser(FriendRequest request) =>
            OnYouSentFriendRequestToOtherUser?.Invoke(request);

        public void BroadcastThatYouRemovedFriend(string userId) =>
            OnYouRemovedFriend?.Invoke(userId);

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

        public void BroadcastThatOtherUserRemovedTheFriendship(string userId) =>
            OnOtherUserRemovedTheFriendship?.Invoke(userId);

        public void BroadcastFriendConnected(FriendProfile friend) =>
            OnFriendConnected?.Invoke(friend);

        public void BroadcastFriendDisconnected(FriendProfile friend) =>
            OnFriendDisconnected?.Invoke(friend);

        public void BroadcastFriendAsAway(FriendProfile friend) =>
            OnFriendAway?.Invoke(friend);

        public void BroadcastYouBlockedProfile(BlockedProfile profile) =>
            OnYouBlockedProfile?.Invoke(profile);

        public void BroadcastYouUnblockedProfile(BlockedProfile profile) =>
            OnYouUnblockedProfile?.Invoke(profile);

        public void BroadcastOtherUserBlockedYou(string userId) =>
            OnYouBlockedByUser?.Invoke(userId);

        public void BroadcastOtherUserUnblockedYou(string userId) =>
            OnYouUnblockedByUser?.Invoke(userId);
    }
}
