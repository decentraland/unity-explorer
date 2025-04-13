using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Settings.Settings;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat
{
    public class ChatUserStateUpdater : IDisposable
    {
        private const int CONNECTION_CHECK_INTERVAL_MS = 30000;
        private const string PRIVACY_SETTING_ALL = "all";

        private readonly IChatUsersStateCache chatUsersStateCache;
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
        private readonly HashSet<string> chatUsers = new ();

        private CancellationTokenSource cts = new ();
        private bool roomConnected;
        private string currentConversation = string.Empty;
        private bool checkedUsers;
        private bool isDisposed;

        public ChatUserStateUpdater(
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IParticipantsHub participantsHub,
            ChatSettingsAsset settingsAsset,
            RPCChatPrivacyService rpcChatPrivacyService,
            IChatUserStateEventBus chatUserStateEventBus,
            IChatUsersStateCache chatUsersStateCache,
            IFriendsEventBus friendsEventBus,
            IRoom chatRoom,
            ObjectProxy<IFriendsService> friendsService)
        {
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.participantsHub = participantsHub;
            this.settingsAsset = settingsAsset;
            this.rpcChatPrivacyService = rpcChatPrivacyService;
            this.chatUserStateEventBus = chatUserStateEventBus;
            this.chatUsersStateCache = chatUsersStateCache;
            this.friendsEventBus = friendsEventBus;
            this.chatRoom = chatRoom;
            this.friendsService = friendsService;

            SubscribeToEvents();
        }

        public string CurrentConversation
        {
            set => currentConversation = value;
        }

        public async UniTask<HashSet<string>> Initialize(IEnumerable<ChatChannel.ChannelId> openConversations)
        {
            this.openConversations.Clear();

            foreach (ChatChannel.ChannelId conversation in openConversations)
                this.openConversations.Add(conversation.Id);

            var conversationParticipants = new HashSet<string>();

            try
            {
                await rpcChatPrivacyService.GetOwnSocialSettingsAsync(cts.Token);
                await UniTask.WaitUntil(() => roomConnected && userBlockingCacheProxy.Configured, cancellationToken: cts.Token);

                await ProcessInitialParticipants(conversationParticipants);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, $"Error during initialization: {e.Message}");
            }

            return conversationParticipants;
        }

        private async UniTask ProcessInitialParticipants(HashSet<string> conversationParticipants)
        {
            foreach (string participant in participantsHub.RemoteParticipantIdentities())
            {
                if (userBlockingCacheProxy.StrictObject.UserIsBlocked(participant))
                    chatUsersStateCache.AddConnectedBlockedUser(participant);
                else
                {
                    chatUsersStateCache.AddConnectedUser(participant);

                    if (openConversations.Contains(participant))
                        conversationParticipants.Add(participant);
                }
            }
        }

        public async UniTask GetConversationUsersConnectedStatus()
        {
            if (!checkedUsers) return;

            checkedUsers = false;
            try
            {
                ProcessConnectedUsers();
                await UniTask.Delay(CONNECTION_CHECK_INTERVAL_MS, cancellationToken: cts.Token);
                checkedUsers = true;
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, $"Error checking user status: {e.Message}");
            }
        }

        private void ProcessConnectedUsers()
        {
            var connectedParticipants = new HashSet<string>(participantsHub.RemoteParticipantIdentities());

            foreach (string participant in openConversations)
            {
                if (connectedParticipants.Contains(participant))
                {
                    if (userBlockingCacheProxy.StrictObject.UserIsBlocked(participant))
                        chatUsersStateCache.AddConnectedBlockedUser(participant);
                    else
                        chatUsersStateCache.AddConnectedUser(participant);

                }
                else
                {
                    chatUsersStateCache.RemoveConnectedUser(participant);
                    chatUsersStateCache.RemovedConnectedBlockedUser(participant);
                }
            }
        }

        public async UniTask<ChatUserState> GetChatUserStateAsync(string userId, CancellationToken ct)
        {
            await GetConversationUsersConnectedStatus();
            var friendshipStatus = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, ct);

            //If it's a friend we just return its connection status
            if (friendshipStatus == FriendshipStatus.FRIEND)
                return chatUsersStateCache.IsUserConnected(userId) ? ChatUserState.CONNECTED : ChatUserState.DISCONNECTED;

            //If the user is blocked by us, we show that first
            if (friendshipStatus == FriendshipStatus.BLOCKED)
                return ChatUserState.BLOCKED_BY_OWN_USER;

            if (friendshipStatus == FriendshipStatus.BLOCKED_BY ||
                !chatUsersStateCache.IsUserConnected(userId))
                return ChatUserState.DISCONNECTED;

            //If the user is connected we need to check our settings and then theirs.
            if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER;

            //If we allow ALL messages, we need to know their settings.
            chatUsers.Clear();
            chatUsers.Add(userId);
            var response = await rpcChatPrivacyService.GetPrivacySettingForUsersAsync(chatUsers, cts.Token);

            if (response[0].Count > 0)
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

            if (response[0].Count > 0)
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

        //TODO FRAN: check if we need to re-add this in case the connection is terminated when switching profiles.
        private void OnChatRoomConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate)
        {
            if (connectionUpdate == ConnectionUpdate.Connected)
            {
                chatRoom.ConnectionUpdated -= OnChatRoomConnectionUpdated;
                roomConnected = true;
            }
        }

        private async UniTaskVoid RequestParticipantsPrivacySettings(HashSet<string> participants, CancellationToken ct)
        {
            var onlyFriendsParticipants = await rpcChatPrivacyService.GetPrivacySettingForUsersAsync(participants, ct);
            foreach (string participant in onlyFriendsParticipants[0])
            {
                if (currentConversation == participant)
                {
                    chatUserStateEventBus.OnCurrentConversationUserUnavailable();
                    return;
                }
            }

            foreach (string participant in onlyFriendsParticipants[1])
            {
                if (currentConversation == participant)
                {
                    chatUserStateEventBus.OnCurrentConversationUserAvailable();
                    return;
                }
            }
        }

        private void OnPrivacySettingsSet(ChatPrivacySettings privacySettings)
        {
            cts = cts.SafeRestart();
            rpcChatPrivacyService.UpsertSocialSettingsAsync(privacySettings == ChatPrivacySettings.ALL, cts.Token).Forget();

            if (privacySettings == ChatPrivacySettings.ALL)
            {
                chatUsers.Clear();
                chatUsers.Add(currentConversation);
                RequestParticipantsPrivacySettings(chatUsers, cts.Token).Forget();
            }
        }

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Update From Participant!!! {participant.Metadata}");

            switch (update)
            {
                case UpdateFromParticipant.Connected:
                    //If the user is not blocked, we add it as a connected user, then check if its a friend, otherwise, we add it as a blocked user
                    if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(participant.Identity))
                    {
                        chatUsersStateCache.AddConnectedUser(participant.Identity);

                        if (openConversations.Contains(participant.Identity))
                            CheckFriendStatus(participant.Identity).Forget();
                    }
                    else
                    {
                        //If the user is blocked (or blocking us) we consider them as if they remained offline.
                        chatUsersStateCache.AddConnectedBlockedUser(participant.Identity);
                    }
                    break;
                case UpdateFromParticipant.MetadataChanged:
                    ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Metadata Changed!!! {participant.Metadata}");

                    if (currentConversation != participant.Identity) return;
                    if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS) return;
                    if (userBlockingCacheProxy.StrictObject.UserIsBlocked(participant.Identity)) return;

                    ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Metadata Changed - Passed all checks!!! {participant.Metadata}");

                    //We only care about their data if it's the current conversation, we allow messages from ALL and the user it's not blocked.
                    CheckUserMetadata(participant).Forget();
                    break;
                case UpdateFromParticipant.Disconnected:
                    chatUsersStateCache.RemoveConnectedUser(participant.Identity);
                    chatUsersStateCache.RemovedConnectedBlockedUser(participant.Identity);

                    if (!openConversations.Contains(participant.Identity)) return;

                    if (userBlockingCacheProxy.StrictObject.BlockedUsers.Contains(participant.Identity))
                    {
                        chatUserStateEventBus.OnUserBlocked(participant.Identity);
                        return;
                    }

                    chatUserStateEventBus.OnUserDisconnected(participant.Identity);
                    break;
            }
        }

        private async UniTaskVoid CheckFriendStatus(string userId)
        {
            var friendshipStatus = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, cts.Token);
            if (friendshipStatus == FriendshipStatus.FRIEND)
                chatUserStateEventBus.OnFriendConnected(userId);
            else
                chatUserStateEventBus.OnNonFriendConnected(userId);
        }

        private async UniTaskVoid CheckUserMetadata(Participant participant)
        {
            var message = JsonUtility.FromJson<ParticipantPrivacyMetadata>(participant.Metadata);

            ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Read metadata from user {participant.Metadata}");

            //If they accept all conversations, we dont need to check if they are friends or not
            //TODO FRAN: Convert this into a CONST
            if (message.private_messages_privacy == PRIVACY_SETTING_ALL)
            {
                chatUserStateEventBus.OnCurrentConversationUserAvailable();
                return;
            }

            var status = await friendsService.StrictObject.GetFriendshipStatusAsync(participant.Identity, cts.Token);
            if (status == FriendshipStatus.FRIEND) return;

            chatUserStateEventBus.OnCurrentConversationUserUnavailable();
        }

        private void OnFriendRemoved(string userid)
        {
            if (!chatUsersStateCache.IsUserConnected(userid)) return;

            if (openConversations.Contains(userid))
                chatUserStateEventBus.OnNonFriendConnected(userid);
        }

        private void OnNewFriendAdded(string userid)
        {
            if (!chatUsersStateCache.IsUserConnected(userid)) return;

            if (openConversations.Contains(userid))
                chatUserStateEventBus.OnFriendConnected(userid);
        }

        private void OnUserUnblocked(string userid)
        {
            chatUsersStateCache.RemovedConnectedBlockedUser(userid);
            chatUsersStateCache.AddConnectedUser(userid);

            if (openConversations.Contains(userid))
                chatUserStateEventBus.OnNonFriendConnected(userid);
        }

        private void OnYouUnblockedProfile(BlockedProfile profile)
        {
            if (!chatUsersStateCache.IsBlockedUserConnected(profile.Address))
            {
                if (openConversations.Contains(profile.Address))
                    chatUserStateEventBus.OnUserDisconnected(profile.Address);
                return;
            }

            OnUserUnblocked(profile.Address);
        }

        private void OnYouBlockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address;
            if (chatUsersStateCache.IsUserConnected(userId))
            {
                chatUsersStateCache.RemoveConnectedUser(userId);
                chatUsersStateCache.AddConnectedBlockedUser(userId);
            }

            if (openConversations.Contains(userId))
                chatUserStateEventBus.OnUserBlocked(userId);
        }

        private void OnYouBlockedByUser(string userId)
        {
            if (!chatUsersStateCache.IsUserConnected(userId)) return;

            chatUsersStateCache.RemoveConnectedUser(userId);
            chatUsersStateCache.AddConnectedBlockedUser(userId);

            if (openConversations.Contains(userId))
                chatUserStateEventBus.OnUserDisconnected(userId);
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
            chatRoom.ConnectionUpdated += OnChatRoomConnectionUpdated;
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
            chatRoom.ConnectionUpdated -= OnChatRoomConnectionUpdated;
        }

        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            cts.Cancel();
            cts.Dispose();
            UnsubscribeFromEvents();
        }
    }
}
