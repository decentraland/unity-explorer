﻿using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Optimization.Pools;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
using LiveKit.Proto;
using System.Linq;
using Utility;
using Utility.Types;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Responsible for managing the private conversation state.
    ///     Always enabled
    /// </summary>
    public class PrivateConversationUserStateService : IDisposable, ICurrentChannelUserStateService
    {
        public enum ChatUserState
        {
            CONNECTED, //Online friends and other users that are not blocked if both users have ALL set in privacy setting.
            BLOCKED_BY_OWN_USER, //Own user blocked the other user
            PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER, //Own user has privacy settings set to ONLY FRIENDS
            PRIVATE_MESSAGES_BLOCKED, //The other user has its privacy settings set to ONLY FRIENDS
            DISCONNECTED, //The other user is either offline or has blocked the own user.
        }

        private const string PRIVACY_SETTING_ALL = "all";
        private const int TIMEOUT_FRIENDS_CONTAINER_MINUTES = 2;

        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ObjectProxy<IFriendsService> friendsService;

        private readonly IParticipantsHub participantsHub;
        private readonly ChatSettingsAsset settingsAsset;
        private readonly RPCChatPrivacyService rpcChatPrivacyService;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly IRoom chatRoom;

        private readonly IEventBus eventBus;
        private readonly CurrentChannelService currentChannelService;

        private readonly HashSet<string> friendIds = new();
        private bool isFriendCacheInitialized  ;

        /// <summary>
        ///     Contains the list of all participants in all private conversations as they share the same LiveKit room
        /// </summary>
        private readonly HashSet<string> onlineParticipants = new (PoolConstants.AVATARS_COUNT);

        private CancellationTokenSource cts = new ();

        public IReadOnlyCollection<string> OnlineParticipants { get; }

        public PrivateConversationUserStateService(CurrentChannelService currentChannelService, IEventBus eventBus, ObjectProxy<IUserBlockingCache> userBlockingCacheProxy, ObjectProxy<IFriendsService> friendsService,
            ChatSettingsAsset settingsAsset, RPCChatPrivacyService rpcChatPrivacyService, IFriendsEventBus friendsEventBus, IRoom chatRoom)
        {
            this.currentChannelService = currentChannelService;
            this.eventBus = eventBus;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.friendsService = friendsService;
            participantsHub = chatRoom.Participants;
            this.settingsAsset = settingsAsset;
            this.rpcChatPrivacyService = rpcChatPrivacyService;
            this.friendsEventBus = friendsEventBus;
            this.chatRoom = chatRoom;
            OnlineParticipants = new ReadOnlyHashSet<string>(onlineParticipants);
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            UnsubscribeFromEvents();
        }

        public void Activate() { }

        public async UniTask<HashSet<string>> InitializeAsync(CancellationToken ct)
        {
            SubscribeToEvents();

            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var conversationParticipants = new HashSet<string>();

            try
            {
                await UniTask.WhenAll(
                    rpcChatPrivacyService.GetOwnSocialSettingsAsync(cts.Token),
                    InitializeFriendshipCacheAsync(cts.Token)
                );

                await UniTask.WaitUntil(() =>
                    chatRoom.Info.ConnectionState == ConnectionState.ConnConnected &&
                    userBlockingCacheProxy.Configured, cancellationToken: cts.Token)
                             .Timeout(TimeSpan.FromMinutes(TIMEOUT_FRIENDS_CONTAINER_MINUTES));

                foreach ((string remoteParticipantIdentity, _) in chatRoom.Participants.RemoteParticipantIdentities().Where(rp => UserIsConsideredAsOnline(rp.Key, true)))
                    onlineParticipants.Add(remoteParticipantIdentity);
            }
            catch (TimeoutException) { ReportHub.LogError(ReportCategory.CHAT_MESSAGES, "Friend service and user blocking cache are not available. Ignore this if you are in LSD"); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogError(ReportCategory.CHAT_MESSAGES, $"Error during initialization: {e.Message}"); }

            return conversationParticipants;
        }

        private void SubscribeToEvents()
        {
            settingsAsset.PrivacySettingsSet += OnPrivacySettingsSet;
            chatRoom.ConnectionUpdated += OnRoomConnectionStateChanged;
            chatRoom.Participants.UpdatesFromParticipant += OnUpdatesFromParticipant;
            friendsEventBus.OnYouBlockedByUser += OnYouBlockedByUser;
            friendsEventBus.OnYouUnblockedByUser += OnUserUnblocked;
            friendsEventBus.OnYouBlockedProfile += OnYouBlockedProfile;
            friendsEventBus.OnYouUnblockedProfile += OnYouUnblockedProfile;
            friendsEventBus.OnOtherUserAcceptedYourRequest += OnNewFriendAdded;
            friendsEventBus.OnOtherUserRemovedTheFriendship += OnFriendRemoved;
            friendsEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += OnNewFriendAdded;
            friendsEventBus.OnYouRemovedFriend += OnFriendRemoved;
        }

        private void UnsubscribeFromEvents()
        {
            settingsAsset.PrivacySettingsSet -= OnPrivacySettingsSet;
            chatRoom.ConnectionUpdated -= OnRoomConnectionStateChanged;
            chatRoom.Participants.UpdatesFromParticipant -= OnUpdatesFromParticipant;
            friendsEventBus.OnYouBlockedByUser -= OnYouBlockedByUser;
            friendsEventBus.OnYouUnblockedByUser -= OnUserUnblocked;
            friendsEventBus.OnYouBlockedProfile -= OnYouBlockedProfile;
            friendsEventBus.OnYouUnblockedProfile -= OnYouUnblockedProfile;
            friendsEventBus.OnOtherUserAcceptedYourRequest -= OnNewFriendAdded;
            friendsEventBus.OnOtherUserRemovedTheFriendship -= OnFriendRemoved;
            friendsEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= OnNewFriendAdded;
            friendsEventBus.OnYouRemovedFriend -= OnFriendRemoved;
        }

        private async UniTask InitializeFriendshipCacheAsync(CancellationToken ct)
        {
            // Implementation requires calling GetFriendsAsync in a loop until all pages are fetched
            // (See the previous detailed answer for the full pagination logic)

            // Example of updating the cache once friends are fetched:
            var allFriends = await GetAllFriendsAsync(ct); // A helper method that handles pagination

            lock (friendIds)
            {
                friendIds.Clear();
                foreach (var friend in allFriends)
                {
                    friendIds.Add(friend.Address);
                }
            }

            isFriendCacheInitialized = true;
        }

        private async UniTask<List<FriendProfile>> GetAllFriendsAsync(CancellationToken ct)
        {
            var allFriends = new List<FriendProfile>();
            if (!friendsService.Configured) return allFriends;

            int pageNum = 0;
            const int pageSize = 100;

            try
            {
                while (true)
                {
                    using var result =
                        await friendsService.StrictObject.GetFriendsAsync(pageNum, pageSize, ct);

                    if (ct.IsCancellationRequested) break;
                    if (result.Friends.Count == 0) break;

                    allFriends.AddRange(result.Friends);

                    if (allFriends.Count >= result.TotalAmount)
                        break;

                    pageNum++;
                }
            }
            // Gracefully handle cancellation on exit without logging it as an error.
            catch (OperationCanceledException)
            {
                // This is expected when the application closes.
            }
            // Catch any other exception, which indicates a real problem.
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.FRIENDS, $"Failed to fetch all friends due to: {ex.Message}");
            }


            return allFriends;
        }

        private void OnPrivacySettingsSet(ChatPrivacySettings privacySettings)
        {
            rpcChatPrivacyService.UpsertSocialSettingsAsync(privacySettings == ChatPrivacySettings.ALL, cts.Token).Forget();

            // Simply notify that the ChatUserState should be updated
            // It will be retrieved via "GetChatUserStateAsync"

            eventBus.Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());
        }

        public async UniTask<ChatUserState> GetChatUserStateAsync(string userId, CancellationToken ct)
        {
            FriendshipStatus friendshipStatus = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, ct);
            Participant? participant = chatRoom.Participants.RemoteParticipant(userId);
            bool isUserConnected = participant != null;

            //If it's a friend we just return its connection status
            if (friendshipStatus == FriendshipStatus.FRIEND)
                return isUserConnected ? ChatUserState.CONNECTED : ChatUserState.DISCONNECTED;

            //If the user is blocked by us, we show that first
            if (friendshipStatus == FriendshipStatus.BLOCKED)
                return ChatUserState.BLOCKED_BY_OWN_USER;

            if (friendshipStatus == FriendshipStatus.BLOCKED_BY || !isUserConnected)
                return ChatUserState.DISCONNECTED;

            //At this point we know the user is connected

            //If the user is connected we need to check our settings and then theirs.
            if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER;

            //If we allow ALL messages, we need to know their settings.
            ParticipantPrivacyMetadata message = JsonUtility.FromJson<ParticipantPrivacyMetadata>(participant!.Metadata);

            if (message.private_messages_privacy != PRIVACY_SETTING_ALL)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED;

            return UserIsConsideredAsOnline(userId, true) ? ChatUserState.CONNECTED : ChatUserState.DISCONNECTED;
        }

        private void OnRoomConnectionStateChanged(IRoom room, ConnectionUpdate connectionUpdate)
        {
            lock (onlineParticipants)
            {
                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        onlineParticipants.Clear();

                        foreach ((string remoteParticipantIdentity, _) in chatRoom.Participants.RemoteParticipantIdentities())
                        {
                            if (UserIsConsideredAsOnline(remoteParticipantIdentity, true))
                                onlineParticipants.Add(remoteParticipantIdentity);
                        }

                        NotifyChannelUsersStateUpdated();

                        break;
                    case ConnectionUpdate.Disconnected:
                        onlineParticipants.Clear();
                        NotifyChannelUsersStateUpdated();
                        break;
                }
            }
        }

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Update From Participant {update.ToString()}");
            string userId = participant.Identity;

            switch (update)
            {
                case UpdateFromParticipant.Connected:
                    //If the user is not blocked, we add it as a connected user, then
                    //check if its a friend, otherwise, we add it as a blocked user
                    ChangeOnlineStatusAndNotify(userId, true, false);
                    break;
                case UpdateFromParticipant.MetadataChanged:
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Metadata Changed {participant.Metadata}");

                    if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS) return;
                    if (currentChannelService.CurrentChannelId.Id != userId) return;
                    if (userBlockingCacheProxy.StrictObject.UserIsBlocked(userId)) return;

                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, "Metadata Changed - Passed all checks");

                    //We only care about their data if it's the current conversation, we allow messages from ALL and the user it's not blocked.

                    CheckUserMetadataAsync(userId, participant.Metadata).Forget();
                    break;
                case UpdateFromParticipant.Disconnected:
                    onlineParticipants.Remove(userId);
                    NotifyUserStateUpdated(userId, false);
                    break;
            }
        }

        private async UniTaskVoid CheckUserMetadataAsync(string userId, string metadata)
        {
            ParticipantPrivacyMetadata message = JsonUtility.FromJson<ParticipantPrivacyMetadata>(metadata);

            //If they accept all conversations, we dont need to check if they are friends or not
            if (message.private_messages_privacy == PRIVACY_SETTING_ALL)
            {
                ChangeOnlineStatusAndNotify(userId, UserIsConnectedToRoom(userId), true);
                return;
            }

            Result<FriendshipStatus> status = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, cts.Token).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (cts.IsCancellationRequested)
                return;

            if (status is { Success: true, Value: FriendshipStatus.FRIEND }) return;

            ChangeOnlineStatusAndNotify(userId, UserIsConnectedToRoom(userId), false);
        }

        private void OnFriendRemoved(string userId)
        {
            lock (friendIds) { friendIds.Remove(userId); }

            if (!UserIsConnectedToRoom(userId)) return;

            ChangeOnlineStatusAndNotify(userId, true, false);
        }

        private void OnNewFriendAdded(string userId)
        {
            lock (friendIds) { friendIds.Add(userId); }

            if (!UserIsConnectedToRoom(userId)) return;

            ChangeOnlineStatusAndNotify(userId, true, true);
        }

        private void OnUserUnblocked(string userId)
        {
            if (!UserIsConnectedToRoom(userId)) return;

            ChangeOnlineStatusAndNotify(userId, true, true);
        }

        private void OnYouUnblockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address.ToString();

            bool userConnected = UserIsConsideredAsOnline(userId, UserIsConnectedToRoom(userId));

            ChangeOnlineStatusAndNotify(userId, userConnected, true);
        }

        private void OnYouBlockedByUser(string userId)
        {
            if (!UserIsConnectedToRoom(userId)) return;

            ChangeOnlineStatusAndNotify(userId, true, false);
        }

        private void OnYouBlockedProfile(BlockedProfile profile)
        {
            Web3Address userId = profile.Address;

            ChangeOnlineStatusAndNotify(userId, UserIsConnectedToRoom(userId), false);
        }

        /// <summary>
        /// Synchronous version of GetChatUserStateAsync
        /// Determines if a given user should be considered "online"
        /// based on both parties' privacy settings, friendship status,
        /// and connection state, without performing async service calls.
        /// </summary>
        /// <param name="userId">The ID of the user to check.</param>
        /// <param name="onlineInRoom">True if the user is currently connected to the LiveKit room.</param>
        /// <returns>True if the user is considered online under the privacy rules; otherwise false.</returns>
        private bool UserIsConsideredAsOnline(string userId, bool onlineInRoom)
        {
            // 1. Quick reject: not connected to the room
            if (!onlineInRoom)
                return false;

            // 2. Quick reject: we have them blocked
            if (userBlockingCacheProxy.Configured && userBlockingCacheProxy.StrictObject.UserIsBlocked(userId))
                return false;

            // 3. Check friendship cache (thread-safe)
            bool isFriend;
            lock (friendIds)
            {
                isFriend = isFriendCacheInitialized && friendIds.Contains(userId);
            }

            // 4. Apply MY privacy setting
            // If I allow only friends, and this user is not a friend, they appear offline
            if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS && !isFriend)
                return false;

            // 5. Apply THEIR privacy setting (from participant metadata)
            var participant = chatRoom.Participants.RemoteParticipant(userId);
            if (participant != null && !string.IsNullOrEmpty(participant.Metadata))
            {
                ParticipantPrivacyMetadata metadata;
                try
                {
                    metadata = JsonUtility.FromJson<ParticipantPrivacyMetadata>(participant.Metadata);
                }
                catch
                {
                    return false;
                }

                // If they only allow friends, and I am not their friend, they appear offline
                if (metadata.private_messages_privacy != PRIVACY_SETTING_ALL && !isFriend)
                    return false;
            }

            // 6. Passed all checks -> online
            return true;
        }


        private bool UserIsConnectedToRoom(string userId) =>
            chatRoom.Participants.RemoteParticipant(userId) != null;

        private void NotifyChannelUsersStateUpdated()
        {
            eventBus.Publish(new ChatEvents.ChannelUsersStatusUpdated(ChatChannel.EMPTY_CHANNEL_ID, ChatChannel.ChatChannelType.USER, OnlineParticipants));
        }

        private void ChangeOnlineStatusAndNotify(string userId, bool connectedToRoom, bool? friendshipStatusChangedTo = null)
        {
            bool consideredAsOnline;

            if (friendshipStatusChangedTo.HasValue)
                consideredAsOnline = friendshipStatusChangedTo.Value && connectedToRoom;
            else
                consideredAsOnline = UserIsConsideredAsOnline(userId, connectedToRoom);

            bool notify = consideredAsOnline ? onlineParticipants.Add(userId) : onlineParticipants.Remove(userId);

            if (notify || friendshipStatusChangedTo.HasValue)
                NotifyUserStateUpdated(userId, consideredAsOnline);
        }

        private void NotifyUserStateUpdated(string userId, bool isOnline)
        {
            // This service doesn't know about the current channel list
            // so it's the responsibility of the corresponding presenter to detect if the user is in the list

            eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(new ChatChannel.ChannelId(userId), ChatChannel.ChatChannelType.USER, userId, isOnline));

            if (currentChannelService.CurrentChannelId.Id == userId)
                eventBus.Publish(new ChatEvents.CurrentChannelStateUpdatedEvent());
        }

        void ICurrentChannelUserStateService.Deactivate()
        {
        }

        [Serializable]
        public struct ParticipantPrivacyMetadata
        {
            /// <summary>
            ///     The possible values are "all" or "only_friends"
            /// </summary>
            public string private_messages_privacy;

            public ParticipantPrivacyMetadata(string privacy)
            {
                private_messages_privacy = privacy;
            }

            public override string ToString() =>
                $"(Private Messages Privacy: {private_messages_privacy}";
        }

        public void Reset()
        {
            cts.SafeCancelAndDispose();
            UnsubscribeFromEvents();

            lock (friendIds)
            {
                friendIds.Clear();
            }

            isFriendCacheInitialized = false;

            lock (onlineParticipants)
            {
                onlineParticipants.Clear();
            }
        }
    }
}
