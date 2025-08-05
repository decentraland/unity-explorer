using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
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

        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ObjectProxy<IFriendsService> friendsService;

        private readonly IParticipantsHub participantsHub;
        private readonly ChatSettingsAsset settingsAsset;
        private readonly RPCChatPrivacyService rpcChatPrivacyService;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly IRoom chatRoom;

        private readonly IEventBus eventBus;
        private readonly CurrentChannelService currentChannelService;

        private readonly HashSet<string> onlineParticipants = new (1); // always 1

        private CancellationTokenSource cts = new ();

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

        public ReadOnlyHashSet<string> OnlineParticipants { get; }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            UnsubscribeFromEvents();
        }

        public void Activate(string userId)
        {
            onlineParticipants.Add(userId);
        }

        public async UniTask<HashSet<string>> InitializeAsync(IEnumerable<ChatChannel.ChannelId> openConversations)
        {
            SubscribeToEvents();

            cts = cts.SafeRestart();

            var conversationParticipants = new HashSet<string>();

            try
            {
                await rpcChatPrivacyService.GetOwnSocialSettingsAsync(cts.Token);
                await UniTask.WaitUntil(() => chatRoom.Info.ConnectionState == ConnectionState.ConnConnected && userBlockingCacheProxy.Configured, cancellationToken: cts.Token);

                foreach (ChatChannel.ChannelId openConversation in openConversations)
                {
                    if (participantsHub.RemoteParticipant(openConversation.Id) != null)
                        conversationParticipants.Add(openConversation.Id);
                }
            }
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

            bool isBlocked = userBlockingCacheProxy.Configured && userBlockingCacheProxy.StrictObject.UserIsBlocked(userId);

            return isBlocked ? ChatUserState.DISCONNECTED : ChatUserState.CONNECTED;
        }

        private void OnRoomConnectionStateChanged(IRoom room, ConnectionUpdate connectionUpdate)
        {
            lock (onlineParticipants)
            {
                switch (connectionUpdate)
                {
                    case ConnectionUpdate.Connected:
                        onlineParticipants.Clear();

                        foreach (string remoteParticipantIdentity in chatRoom.Participants.RemoteParticipantIdentities())
                        {
                            if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(remoteParticipantIdentity))
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
                    NotifyUserStateUpdated(userId, !userBlockingCacheProxy.StrictObject.UserIsBlocked(userId));
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
                NotifyUserStateUpdated(userId, true);
                return;
            }

            Result<FriendshipStatus> status = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, cts.Token).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

            if (cts.IsCancellationRequested)
                return;

            if (status is { Success: true, Value: FriendshipStatus.FRIEND }) return;

            NotifyUserStateUpdated(userId, false);
        }

        private void OnFriendRemoved(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            NotifyUserStateUpdated(userId, false);
        }

        private void OnNewFriendAdded(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            NotifyUserStateUpdated(userId, true);
        }

        private void OnUserUnblocked(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            NotifyUserStateUpdated(userId, true);
        }

        private void OnYouUnblockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address.ToString();

            bool userConnected = participantsHub.RemoteParticipant(userId) != null && !userBlockingCacheProxy.StrictObject.UserIsBlocked(userId);

            NotifyUserStateUpdated(userId, userConnected);
        }

        private void OnYouBlockedByUser(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            NotifyUserStateUpdated(userId, false);
        }

        private void OnYouBlockedProfile(BlockedProfile profile)
        {
            Web3Address userId = profile.Address;

            NotifyUserStateUpdated(userId, false);
        }

        private void NotifyChannelUsersStateUpdated()
        {
            eventBus.Publish(new ChatEvents.ChannelUsersStatusUpdated(ChatChannel.EMPTY_CHANNEL_ID, ChatChannel.ChatChannelType.USER, OnlineParticipants));
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
            onlineParticipants.Clear();
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
    }
}
