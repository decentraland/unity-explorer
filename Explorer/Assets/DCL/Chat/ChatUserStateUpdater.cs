using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Settings.Settings;
using DCL.Utilities;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Chat
{
    public class ChatUserStateUpdater
    {
        private readonly IChatUsersStateCache chatUsersStateCache;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ObjectProxy<FriendsCache> friendsCacheProxy;
        private readonly IParticipantsHub participantsHub;
        private readonly ChatSettingsAsset settingsAsset;
        private readonly RPCChatPrivacyService rpcChatPrivacyService;
        private readonly IChatUserStateEventBus chatUserStateEventBus;


        /// <summary>
        /// We will use this to track which conversations are open and decide if its necessary to notify the controller about changes
        /// </summary>
        private readonly HashSet<string> openConversations = new ();

        private CancellationTokenSource cts;


        public ChatUserStateUpdater(
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ObjectProxy<FriendsCache> friendsCacheProxy,
            IParticipantsHub participantsHub,
            ChatSettingsAsset settingsAsset,
            RPCChatPrivacyService rpcChatPrivacyService,
            IChatUserStateEventBus chatUserStateEventBus,
            IChatUsersStateCache chatUsersStateCache)
        {
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.friendsCacheProxy = friendsCacheProxy;
            this.participantsHub = participantsHub;
            this.settingsAsset = settingsAsset;
            this.rpcChatPrivacyService = rpcChatPrivacyService;
            this.chatUserStateEventBus = chatUserStateEventBus;
            this.chatUsersStateCache = chatUsersStateCache;

            settingsAsset.PrivacySettingsSet += OnPrivacySettingsSet;
            settingsAsset.PrivacySettingsRead += OnPrivacySettingsRead;
            participantsHub.UpdatesFromParticipant += OnUpdatesFromParticipant;
            //TODO FRAN: We need to subscribe to block and friends events, to update our caches properly, at least blocked, as it affects connection status.
        }


        public IEnumerable<string> Initialize(IEnumerable<string> openConversations)
        {
            this.openConversations.Clear();

            foreach (string conversation in openConversations)
                this.openConversations.Add(conversation);

            var connectedParticipants = new List<string>();


            rpcChatPrivacyService.GetOwnSocialSettingsAsync(cts.Token).Forget();

            if (!friendsCacheProxy.Configured) return connectedParticipants; //We should return full list
            if (!userBlockingCacheProxy.Configured) return connectedParticipants; //We should return full list or similar

            var participants = new HashSet<string>();

            foreach (string participant in participantsHub.RemoteParticipantIdentities())
            {
                if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(participant))
                {
                    if (friendsCacheProxy.StrictObject.Contains(participant))
                        chatUsersStateCache.AddConnectedFriend(participant);
                    else
                        chatUsersStateCache.AddConnectedNonFriend(participant);

                    if (this.openConversations.Contains(participant))
                    {
                        //When this finishes, we have a proper list of all connected users, so we can setup the conversations sidebar UI
                        connectedParticipants.Add(participant);
                    }

                    participants.Add(participant);
                }

                // We only care about other users privacy settings if we can actually talk to them,
                // If we dont allow chatting to ALL users, we wont make any request.
                if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ALL)
                {
                    cts = cts.SafeRestart();
                    RequestParticipantsPrivacySettings(participants, cts.Token).Forget();
                }
            }

            return connectedParticipants;
        }

        public ChatUserState GetChatUserState(string userId)
        {
            //If it's a friend we just return its status
            if (friendsCacheProxy.StrictObject.Contains(userId))
                return chatUsersStateCache.IsFriendConnected(userId) ? ChatUserState.Connected : ChatUserState.Disconnected;

            //If it's not a friend, we check if its connected and depending on that, we check if we can actually write to them or not.
            if (chatUsersStateCache.IsNonFriendConnected(userId))
            {
                if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS)
                    return ChatUserState.PrivateMessagesBlockedByOwnUser;

                //TODO FRAN: here we should do a request to BE checking if the user accepts messages from non-friends

                if (chatUsersStateCache.IsUserUnavailableToChat(userId))
                    return ChatUserState.PrivateMessagesBlocked;

                return ChatUserState.Connected;
            }

            //If user isn't connected, it means it's either offline (or its blocking us) or we are blocking them, in which case we show different text.
            if (userBlockingCacheProxy.StrictObject.BlockedByUsers.Contains(userId))
                return ChatUserState.BlockedByOwnUser;

            return ChatUserState.Disconnected;
        }

        public enum ChatUserState
        {
            Connected, //Online friends and other users that are not blocked if both users have ALL set in privacy setting.
            BlockedByOwnUser, //Own user blocked the other user
            PrivateMessagesBlockedByOwnUser, //Own user has privacy settings set to ONLY FRIENDS
            PrivateMessagesBlocked, //The other user has its privacy settings set to ONLY FRIENDS
            Disconnected //The other user is either offline or has blocked the own user.
        }

        public void AddConversation(string conversationId)
        {
            openConversations.Add(conversationId);
        }

        public void RemoveConversation(string conversationId)
        {
            openConversations.Remove(conversationId);
        }

        private async UniTaskVoid RequestParticipantsPrivacySettings(HashSet<string> participants, CancellationToken ct)
        {
            var onlyFriendsParticipants = await rpcChatPrivacyService.GetPrivacySettingForUsersAsync(participants, ct);

            chatUsersStateCache.AddUsersUnavailableToChat(onlyFriendsParticipants[0]);
            chatUsersStateCache.RemoveUsersUnavailableToChat(onlyFriendsParticipants[1]);

            foreach (string participant in onlyFriendsParticipants[0])
            {
                if (openConversations.Contains(participant))
                    chatUserStateEventBus.OnUserUnavailableToChat(participant);
            }

            foreach (string participant in onlyFriendsParticipants[1])
            {
                if (openConversations.Contains(participant))
                    chatUserStateEventBus.OnUserAvailableToChat(participant);

            }
        }

        private void OnPrivacySettingsRead(ChatPrivacySettings privacySettings)
        {
            UpdateOwnMetadata(privacySettings);
        }

        private void OnPrivacySettingsSet(ChatPrivacySettings privacySettings)
        {
            cts = cts.SafeRestart();
            rpcChatPrivacyService.UpsertSocialSettingsAsync(privacySettings == ChatPrivacySettings.ALL, cts.Token).Forget();
            UpdateOwnMetadata(privacySettings);

            if (privacySettings == ChatPrivacySettings.ALL)
                RequestParticipantsPrivacySettings(chatUsersStateCache.ConnectedNonFriends, cts.Token).Forget();

        }

        private void UpdateOwnMetadata(ChatPrivacySettings privacySettings)
        {
            //To update our metadata we can use Room.UpdateLocalMetadata -> we probably need to do this when switching our settings and when first reading them.
            //Then the other clients receive a notification of metadata updated and update the cache accordingly.
        }

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            switch (update)
            {
                //If the user is a friend, we add it to the connected friends hashset, if its not, we need to check if its blocked.
                case UpdateFromParticipant.Connected:
                    if (friendsCacheProxy.StrictObject.Contains(participant.Identity))
                    {
                        chatUsersStateCache.AddConnectedFriend(participant.Identity);

                        if (openConversations.Contains(participant.Identity))
                        {
                            chatUserStateEventBus.OnFriendConnected(participant.Identity);
                        }
                    }
                    else if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(participant.Identity))
                    {
                        chatUsersStateCache.AddConnectedNonFriend(participant.Identity);
                        if (openConversations.Contains(participant.Identity))
                        {
                            chatUserStateEventBus.OnNonFriendConnected(participant.Identity);
                        }
                    }
                    break;
                case UpdateFromParticipant.MetadataChanged:
                    //Parse metadata and if its not a friend and its not blocked and its set as not allowing, add to the list
                    break;
                case UpdateFromParticipant.Disconnected:
                    if (friendsCacheProxy.StrictObject.Contains(participant.Identity))
                    {
                        chatUsersStateCache.RemoveConnectedFriend(participant.Identity);
                        if (openConversations.Contains(participant.Identity))
                            chatUserStateEventBus.OnFriendDisconnected(participant.Identity);
                    }
                    else
                    {
                        chatUsersStateCache.RemoveConnectedNonFriend(participant.Identity);
                        if (openConversations.Contains(participant.Identity))
                            chatUserStateEventBus.OnNonFriendDisconnected(participant.Identity);
                    }
                    break;
            }

        }

    }
}
