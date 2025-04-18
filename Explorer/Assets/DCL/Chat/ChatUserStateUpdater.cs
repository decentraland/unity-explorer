using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Settings.Settings;
using DCL.Utilities;
using LiveKit.Proto;
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

        public async UniTask<HashSet<string>> InitializeAsync(IEnumerable<ChatChannel.ChannelId> openConversations)
        {
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, "UPDATER Initialize Async");

            //SubscribeToEvents();
            isDisposed = false;

            this.openConversations.Clear();

            cts = cts.SafeRestart();

            foreach (ChatChannel.ChannelId conversation in openConversations)
                this.openConversations.Add(conversation.Id);

            ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Open Conversations Loaded! {openConversations}");


            var conversationParticipants = new HashSet<string>();

            try
            {
                await rpcChatPrivacyService.GetOwnSocialSettingsAsync(cts.Token);
                await UniTask.WaitUntil(() => chatRoom.Info.ConnectionState == ConnectionState.ConnConnected && userBlockingCacheProxy.Configured, cancellationToken: cts.Token);

                ProcessInitialParticipants(ref conversationParticipants);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, $"Error during initialization: {e.Message}");
            }

            return conversationParticipants;
        }

        private void ProcessInitialParticipants(ref HashSet<string> conversationParticipants)
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

        public async UniTask<ChatUserState> GetChatUserStateAsync(string userId, CancellationToken ct)
        {
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

            if (response.OnlyFriends.Count > 0)
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
            ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Update From Participant!!! {update.ToString()}");
            string userId = participant.Identity;

            switch (update)
            {
                case UpdateFromParticipant.Connected:
                    //If the user is not blocked, we add it as a connected user, then check if its a friend, otherwise, we add it as a blocked user

                    if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(userId))
                    {
                        chatUsersStateCache.AddConnectedUser(userId);

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
                        chatUsersStateCache.AddConnectedBlockedUser(userId);
                    }
                    break;
                case UpdateFromParticipant.MetadataChanged:
                    ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Metadata Changed!!! {participant.Metadata}");

                    if (CurrentConversation != userId) return;
                    if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS) return;
                    if (userBlockingCacheProxy.StrictObject.UserIsBlocked(userId)) return;

                    ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Metadata Changed - Passed all checks!!! {participant.Metadata}");

                    //We only care about their data if it's the current conversation, we allow messages from ALL and the user it's not blocked.
                    CheckUserMetadataAsync(participant).Forget();
                    break;
                case UpdateFromParticipant.Disconnected:
                    chatUsersStateCache.RemoveConnectedUser(userId);
                    chatUsersStateCache.RemovedConnectedBlockedUser(userId);

                    if (!openConversations.Contains(participant.Identity)) return;

                    chatUserStateEventBus.OnUserConnectionStateChanged(participant.Identity, false);

                    if (CurrentConversation != participant.Identity) return;

                    if (userBlockingCacheProxy.StrictObject.BlockedUsers.Contains(participant.Identity))
                    {
                        chatUserStateEventBus.OnUserBlocked(participant.Identity);
                        return;
                    }

                    chatUserStateEventBus.OnUserDisconnected(participant.Identity);
                    break;
            }
        }

        private async UniTaskVoid CheckFriendStatusAsync(string userId)
        {
            var friendshipStatus = await friendsService.StrictObject.GetFriendshipStatusAsync(userId, cts.Token);
            if (friendshipStatus == FriendshipStatus.FRIEND)
                chatUserStateEventBus.OnFriendConnected(userId);
            else
                chatUserStateEventBus.OnNonFriendConnected(userId);
        }

        private async UniTaskVoid CheckUserMetadataAsync(Participant participant)
        {
            var message = JsonUtility.FromJson<ParticipantPrivacyMetadata>(participant.Metadata);

            ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"Read metadata from user {message}");

            //If they accept all conversations, we dont need to check if they are friends or not
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

            if (CurrentConversation == userid)
                chatUserStateEventBus.OnNonFriendConnected(userid);
        }

        private void OnNewFriendAdded(string userid)
        {
            if (!chatUsersStateCache.IsUserConnected(userid)) return;

            if (CurrentConversation == userid)
                chatUserStateEventBus.OnFriendConnected(userid);
        }

        private void OnUserUnblocked(string userId)
        {
            chatUsersStateCache.RemovedConnectedBlockedUser(userId);
            chatUsersStateCache.AddConnectedUser(userId);

            if (openConversations.Contains(userId))
            {
                chatUserStateEventBus.OnUserConnectionStateChanged(userId, true);

                if (CurrentConversation == userId)
                    chatUserStateEventBus.OnNonFriendConnected(userId);
            }
        }

        private void OnYouUnblockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address.ToString();
            if (openConversations.Contains(userId)) return;

            if (chatUsersStateCache.IsBlockedUserConnected(userId))
            {
                //We need to make sure we are still not blocked by the other user
                if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(userId))
                {
                    OnUserUnblocked(userId);
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
            if (chatUsersStateCache.IsUserConnected(userId))
            {
                chatUsersStateCache.RemoveConnectedUser(userId);
                chatUsersStateCache.AddConnectedBlockedUser(userId);
            }

            if (openConversations.Contains(userId))
            {
                chatUserStateEventBus.OnUserConnectionStateChanged(userId, false);

                if (CurrentConversation == userId)
                    chatUserStateEventBus.OnUserBlocked(userId);

            }
        }

        private void OnYouBlockedByUser(string userId)
        {
            if (!chatUsersStateCache.IsUserConnected(userId)) return;

            chatUsersStateCache.RemoveConnectedUser(userId);
            chatUsersStateCache.AddConnectedBlockedUser(userId);

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
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, "UPDATER Subscribe To Events");
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
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, "UPDATER Unsubscribe to events");
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
            //UnsubscribeFromEvents();
            openConversations.Clear();
            CurrentConversation = string.Empty;
        }
    }
}
