using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Settings.Settings;
using DCL.Utilities;
using LiveKit.Rooms.Participants;
using System;
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
        private readonly ObjectProxy<RPCChatPrivacyService> rpcChatPrivacyService;
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
            ObjectProxy<RPCChatPrivacyService> rpcChatPrivacyService,
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
        }


        public IEnumerable<string> Initialize(IEnumerable<string> openConversations)
        {
            this.openConversations.Clear();

            foreach (string conversation in openConversations)
                this.openConversations.Add(conversation);

            var connectedParticipants = new List<string>();

            if (!rpcChatPrivacyService.Configured) return connectedParticipants;

            rpcChatPrivacyService.StrictObject.GetOwnSocialSettingsAsync(cts.Token).Forget();

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

        public void OnOpenConversation(string conversationId)
        {
            openConversations.Add(conversationId);
        }

        public void OnCloseConversation(string conversationId)
        {
            openConversations.Remove(conversationId);
        }

        private async UniTaskVoid RequestParticipantsPrivacySettings(HashSet<string> participants, CancellationToken ct)
        {
            var onlyFriendsParticipants = await rpcChatPrivacyService.StrictObject.GetPrivacySettingForUsersAsync(participants, ct);

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
            rpcChatPrivacyService.StrictObject.UpsertSocialSettingsAsync(privacySettings == ChatPrivacySettings.ALL, cts.Token).Forget();
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
