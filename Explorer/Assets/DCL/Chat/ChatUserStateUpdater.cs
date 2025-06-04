using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Chat
{
    public class ChatUserStateUpdater : IDisposable
    {
        private const string PRIVACY_SETTING_ALL = "all";
        //private readonly IChatUsersStateCache chatUsersStateCache;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly IParticipantsHub participantsHub;
        private readonly ChatSettingsAsset settingsAsset;
        private readonly RPCChatPrivacyService rpcChatPrivacyService;
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly ObjectProxy<IFriendsService> friendsService;
        private readonly IRoom chatRoom;

        /// <summary>
        /// We will use this to track which conversations are open and decide if its necessary to notify the controller about changes
        /// </summary>
        private readonly HashSet<string> openConversations = new ();
        private readonly HashSet<string> chatUsers = new (1);

        private CancellationTokenSource cts = new ();
        private bool isDisposed;

        public string CurrentConversation { get; set; } = string.Empty;

        public ChatUserStateUpdater(
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IParticipantsHub participantsHub,
            ChatSettingsAsset settingsAsset,
            RPCChatPrivacyService rpcChatPrivacyService,
            IChatUserStateEventBus chatUserStateEventBus,
            IFriendsEventBus friendsEventBus,
            IRoom chatRoom,
            ObjectProxy<IFriendsService> friendsService)
        {
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.participantsHub = participantsHub;
            this.settingsAsset = settingsAsset;
            this.rpcChatPrivacyService = rpcChatPrivacyService;
            this.chatUserStateEventBus = chatUserStateEventBus;
            this.friendsEventBus = friendsEventBus;
            this.chatRoom = chatRoom;
            this.friendsService = friendsService;
        }

        public async UniTask<HashSet<string>> InitializeAsync(IEnumerable<ChatChannel.ChannelId> openConversations)
        {
            SubscribeToEvents();
            isDisposed = false;

            this.openConversations.Clear();

            cts = cts.SafeRestart();

            foreach (ChatChannel.ChannelId conversation in openConversations)
                this.openConversations.Add(conversation.Id);

            var conversationParticipants = new HashSet<string>();

            try
            {
                await rpcChatPrivacyService.GetOwnSocialSettingsAsync(cts.Token);
                await UniTask.WaitUntil(() => chatRoom.Info.ConnectionState == ConnectionState.ConnConnected && userBlockingCacheProxy.Configured, cancellationToken: cts.Token);

                ProcessInitialParticipants(ref conversationParticipants);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogError(ReportCategory.CHAT_MESSAGES, $"Error during initialization: {e.Message}");
            }

            return conversationParticipants;
        }

        private void ProcessInitialParticipants(ref HashSet<string> conversationParticipants)
        {
            foreach (string userId in openConversations)
            {
                if (participantsHub.RemoteParticipant(userId) != null)
                    conversationParticipants.Add(userId);
            }
        }

        public async UniTask<ChatUserState> GetChatUserStateAsync(string userId, CancellationToken ct)
        {
            var friendshipStatus = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, ct);
            var participant = chatRoom.Participants.RemoteParticipant(userId);
            bool isUserConnected = participant != null;

            //If it's a friend we just return its connection status
            if (friendshipStatus == FriendshipStatus.FRIEND)
                return isUserConnected? ChatUserState.CONNECTED : ChatUserState.DISCONNECTED;

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
            var message = JsonUtility.FromJson<ParticipantPrivacyMetadata>(participant!.Metadata);

            if (message.private_messages_privacy != PRIVACY_SETTING_ALL)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED;

            return ChatUserState.CONNECTED;
        }

        public async UniTask<ChatUserState> GetConnectedNonFriendUserStateAsync(string userId)
        {
            if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER;

            chatUsers.Clear();
            chatUsers.Add(userId);
            var response = await rpcChatPrivacyService.GetPrivacySettingForUsersAsync(chatUsers, cts.Token);

            if (response.OnlyFriends.Count > 0)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED;

            return ChatUserState.CONNECTED;
        }

        public ChatUserState GetDisconnectedUserState(string userId)
        {
            if (userBlockingCacheProxy.StrictObject.BlockedUsers.Contains(userId))
                return ChatUserState.BLOCKED_BY_OWN_USER;

            return ChatUserState.DISCONNECTED;
        }

        public void AddConversation(string conversationId)
        {
            openConversations.Add(conversationId);
        }

        public void RemoveConversation(string conversationId)
        {
            openConversations.Remove(conversationId);
        }

        public enum ChatUserState
        {
            CONNECTED, //Online friends and other users that are not blocked if both users have ALL set in privacy setting.
            BLOCKED_BY_OWN_USER, //Own user blocked the other user
            PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER, //Own user has privacy settings set to ONLY FRIENDS
            PRIVATE_MESSAGES_BLOCKED, //The other user has its privacy settings set to ONLY FRIENDS
            DISCONNECTED //The other user is either offline or has blocked the own user.
        }

        private async UniTaskVoid RequestParticipantsPrivacySettingsAsync(HashSet<string> participants, CancellationToken ct)
        {
            var allParticipants = await rpcChatPrivacyService.GetPrivacySettingForUsersAsync(participants, ct);

            if (allParticipants.OnlyFriends.Contains(CurrentConversation))
                chatUserStateEventBus.OnCurrentConversationUserUnavailable();
            else if (allParticipants.All.Contains(CurrentConversation))
                chatUserStateEventBus.OnCurrentConversationUserAvailable();
        }

        private void OnPrivacySettingsSet(ChatPrivacySettings privacySettings)
        {
            rpcChatPrivacyService.UpsertSocialSettingsAsync(privacySettings == ChatPrivacySettings.ALL, cts.Token).Forget();

            if (privacySettings == ChatPrivacySettings.ALL)
            {
                chatUsers.Clear();
                chatUsers.Add(CurrentConversation);
                RequestParticipantsPrivacySettingsAsync(chatUsers, cts.Token).Forget();
            }
        }

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Update From Participant {update.ToString()}");

            if (!userBlockingCacheProxy.Configured) return;

            var userId = participant.Identity;

            switch (update)
            {
                case UpdateFromParticipant.Connected:
                    //If the user is not blocked, we add it as a connected user, then check if its a friend, otherwise, we add it as a blocked user
                    if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(userId))
                    {
                        if (openConversations.Contains(userId))
                        {
                            chatUserStateEventBus.OnUserConnectionStateChanged(userId, true);

                            if (CurrentConversation == userId)
                                CheckFriendStatusAsync(userId).Forget();
                        }
                    }
                    else
                    {
                        //If the user is blocked (or blocking us) we consider them as if they remained offline.
                        chatUserStateEventBus.OnUserConnectionStateChanged(userId, false);
                    }
                    break;
                case UpdateFromParticipant.MetadataChanged:
                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Metadata Changed {participant.Metadata}");

                    if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS) return;
                    if (CurrentConversation != userId) return;
                    if (userBlockingCacheProxy.StrictObject.UserIsBlocked(userId)) return;

                    ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Metadata Changed - Passed all checks");

                    //We only care about their data if it's the current conversation, we allow messages from ALL and the user it's not blocked.

                    CheckUserMetadataAsync(userId, participant.Metadata).Forget();
                    break;
                case UpdateFromParticipant.Disconnected:

                    if (!openConversations.Contains(userId)) return;

                    chatUserStateEventBus.OnUserConnectionStateChanged(userId, false);

                    if (CurrentConversation != userId) return;

                    if (userBlockingCacheProxy.StrictObject.BlockedUsers.Contains(userId))
                    {
                        chatUserStateEventBus.OnUserBlocked(userId);
                        return;
                    }

                    chatUserStateEventBus.OnUserDisconnected(userId);
                    break;
            }
        }

        private async UniTaskVoid CheckFriendStatusAsync(string userId)
        {
            Result<FriendshipStatus> result = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, cts.Token).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);
            if (!result.Success) return;

            if (result.Value == FriendshipStatus.FRIEND)
                chatUserStateEventBus.OnFriendConnected(userId);
            else
                chatUserStateEventBus.OnNonFriendConnected(userId);
        }

        private async UniTaskVoid CheckUserMetadataAsync(string userId, string metadata)
        {
            var message = JsonUtility.FromJson<ParticipantPrivacyMetadata>(metadata);

            //If they accept all conversations, we dont need to check if they are friends or not
            if (message.private_messages_privacy == PRIVACY_SETTING_ALL)
            {
                chatUserStateEventBus.OnUserConnectionStateChanged(userId, true);
                chatUserStateEventBus.OnCurrentConversationUserAvailable();
                return;
            }

            Result<FriendshipStatus> status = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, cts.Token).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);
            if (status is { Success: true, Value: FriendshipStatus.FRIEND }) return;

            chatUserStateEventBus.OnCurrentConversationUserUnavailable();
        }

        private void OnFriendRemoved(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            if (CurrentConversation == userId)
                chatUserStateEventBus.OnNonFriendConnected(userId);
        }

        private void OnNewFriendAdded(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            if (CurrentConversation == userId)
                chatUserStateEventBus.OnFriendConnected(userId);
        }

        private void OnUserUnblocked(string userId)
        {
            if (openConversations.Contains(userId))
            {
                if (participantsHub.RemoteParticipant(userId) == null) return;

                chatUserStateEventBus.OnUserConnectionStateChanged(userId, true);

                if (CurrentConversation == userId)
                    chatUserStateEventBus.OnNonFriendConnected(userId);
            }
        }

        private void OnYouUnblockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address.ToString();

            if (!openConversations.Contains(userId)) return;

            if (participantsHub.RemoteParticipant(userId) != null)
            {
                //We need to make sure we are still not blocked by the other user
                if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(userId))
                {
                    chatUserStateEventBus.OnUserConnectionStateChanged(userId, true);

                    if (CurrentConversation == userId)
                        chatUserStateEventBus.OnNonFriendConnected(userId);

                    return;
                }
            }

            chatUserStateEventBus.OnUserConnectionStateChanged(userId, false);

            if (CurrentConversation == userId)
                chatUserStateEventBus.OnUserDisconnected(userId);
        }

        private void OnYouBlockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address;

            if (openConversations.Contains(userId))
            {
                chatUserStateEventBus.OnUserConnectionStateChanged(userId, false);

                if (CurrentConversation == userId)
                    chatUserStateEventBus.OnUserBlocked(userId);

            }
        }

        private void OnYouBlockedByUser(string userId)
        {
            if (participantsHub.RemoteParticipant(userId) == null) return;

            if (openConversations.Contains(userId))
            {
                chatUserStateEventBus.OnUserConnectionStateChanged(userId, false);

                if (CurrentConversation == userId)
                    chatUserStateEventBus.OnUserDisconnected(userId);
            }
        }

        [Serializable]
        public struct ParticipantPrivacyMetadata
        {
            /// <summary>
            /// The possible values are "all" or "only_friends"
            /// </summary>
            public string private_messages_privacy;
            public ParticipantPrivacyMetadata(string privacy)
            {
                private_messages_privacy = privacy;
            }

            public override string ToString() =>
                $"(Private Messages Privacy: {private_messages_privacy}";
        }

        private void SubscribeToEvents()
        {
            settingsAsset.PrivacySettingsSet += OnPrivacySettingsSet;
            participantsHub.UpdatesFromParticipant += OnUpdatesFromParticipant;
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
            participantsHub.UpdatesFromParticipant -= OnUpdatesFromParticipant;
            friendsEventBus.OnYouBlockedByUser -= OnYouBlockedByUser;
            friendsEventBus.OnYouUnblockedByUser -= OnUserUnblocked;
            friendsEventBus.OnYouBlockedProfile -= OnYouBlockedProfile;
            friendsEventBus.OnYouUnblockedProfile -= OnYouUnblockedProfile;
            friendsEventBus.OnOtherUserAcceptedYourRequest -= OnNewFriendAdded;
            friendsEventBus.OnOtherUserRemovedTheFriendship -= OnFriendRemoved;
            friendsEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser -= OnNewFriendAdded;
            friendsEventBus.OnYouRemovedFriend -= OnFriendRemoved;
        }

        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            cts.SafeCancelAndDispose();
            UnsubscribeFromEvents();
            openConversations.Clear();
            CurrentConversation = string.Empty;
        }
    }
}
