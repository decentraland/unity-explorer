using Cysharp.Threading.Tasks;
using DCL.Chat.History;
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
        private readonly RPCChatPrivacyService rpcChatPrivacyService;
        private readonly IChatUserStateEventBus chatUserStateEventBus;
        private readonly IFriendsEventBus friendsEventBus;


        /// <summary>
        /// We will use this to track which conversations are open and decide if its necessary to notify the controller about changes
        /// </summary>
        private readonly HashSet<string> openConversations = new ();

        private CancellationTokenSource cts = new ();


        public ChatUserStateUpdater(
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ObjectProxy<FriendsCache> friendsCacheProxy,
            IParticipantsHub participantsHub,
            ChatSettingsAsset settingsAsset,
            RPCChatPrivacyService rpcChatPrivacyService,
            IChatUserStateEventBus chatUserStateEventBus,
            IChatUsersStateCache chatUsersStateCache, IFriendsEventBus friendsEventBus)
        {
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.friendsCacheProxy = friendsCacheProxy;
            this.participantsHub = participantsHub;
            this.settingsAsset = settingsAsset;
            this.rpcChatPrivacyService = rpcChatPrivacyService;
            this.chatUserStateEventBus = chatUserStateEventBus;
            this.chatUsersStateCache = chatUsersStateCache;
            this.friendsEventBus = friendsEventBus;

            settingsAsset.PrivacySettingsSet += OnPrivacySettingsSet;
            settingsAsset.PrivacySettingsRead += OnPrivacySettingsRead;
            participantsHub.UpdatesFromParticipant += OnUpdatesFromParticipant;

            //Other user Blocked actions
            friendsEventBus.OnYouBlockedByUser += OnYouBlockedByUser;
            friendsEventBus.OnYouUnblockedByUser += OnUserUnblocked;
            //Own user blocked actions
            friendsEventBus.OnYouBlockedProfile += OnYouBlockedProfile;
            friendsEventBus.OnYouUnblockedProfile += OnYouUnblockedProfile;

            //Other user friendship actions
            friendsEventBus.OnOtherUserAcceptedYourRequest += OnNewFriendAdded;
            friendsEventBus.OnOtherUserRemovedTheFriendship += OnFriendRemoved;
            //Own user friendship actions
            friendsEventBus.OnYouAcceptedFriendRequestReceivedFromOtherUser += OnNewFriendAdded;
            friendsEventBus.OnYouRemovedFriend += OnFriendRemoved;

        }

        public HashSet<string> Initialize(IEnumerable<ChatChannel.ChannelId> openConversations)
        {
            this.openConversations.Clear();

            foreach (ChatChannel.ChannelId conversation in openConversations)
                this.openConversations.Add(conversation.Id);

            var connectedParticipants = new HashSet<string>();


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

        //TODO FRAN: we can optimize it by returning the STATE directly when we call the events on the bus
        public ChatUserState GetChatUserState(string userId)
        {
            //If it's a friend we just return its status
            if (friendsCacheProxy.StrictObject.Contains(userId))
                return chatUsersStateCache.IsFriendConnected(userId) ? ChatUserState.CONNECTED : ChatUserState.DISCONNECTED;

            //If we reach here it's because the user is not a friend

            if (settingsAsset.chatPrivacySettings == ChatPrivacySettings.ONLY_FRIENDS)
                return ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER;

            if (chatUsersStateCache.IsNonFriendConnected(userId))
            {
                if (chatUsersStateCache.IsUserUnavailableToChat(userId))
                    //TODO FRAN: here we should do a request to BE checking if the user accepts messages from non-friends
                    return ChatUserState.PRIVATE_MESSAGES_BLOCKED;

                return ChatUserState.CONNECTED;
            }

            //If user isn't connected, it means it's either offline (or its blocking us) or we are blocking them, in which case we show different text.
            if (userBlockingCacheProxy.StrictObject.BlockedByUsers.Contains(userId))
                return ChatUserState.BLOCKED_BY_OWN_USER;

            return ChatUserState.DISCONNECTED;
        }

        public enum ChatUserState
        {
            CONNECTED, //Online friends and other users that are not blocked if both users have ALL set in privacy setting.
            BLOCKED_BY_OWN_USER, //Own user blocked the other user
            PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER, //Own user has privacy settings set to ONLY FRIENDS
            PRIVATE_MESSAGES_BLOCKED, //The other user has its privacy settings set to ONLY FRIENDS
            DISCONNECTED //The other user is either offline or has blocked the own user.
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
                RequestParticipantsPrivacySettings(chatUsersStateCache.ConnectedNonFriend, cts.Token).Forget();

        }

        private void UpdateOwnMetadata(ChatPrivacySettings privacySettings)
        {
            // TODO FRAN: To update our metadata we can use Room.UpdateLocalMetadata -> we probably need to do this when switching our settings and when first reading them.
            //Then the other clients receive a notification of metadata updated and update the cache accordingly.
        }

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            switch (update)
            {
                //If the user is a friend, we add it to the connected friends hashset, if it's not, we need to check if it's blocked.
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
                    //TODO FRAN: Parse metadata and if it's not a friend and it's not blocked and its set as not allowing, add to the list
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

         private void OnFriendRemoved(string userid)
        {
            if (!chatUsersStateCache.IsFriendConnected(userid)) return;

            chatUsersStateCache.AddConnectedNonFriend(userid);
            chatUsersStateCache.RemoveConnectedFriend(userid);

            if (openConversations.Contains(userid))
                chatUserStateEventBus.OnNonFriendConnected(userid);
        }

        private void OnNewFriendAdded(string userid)
        {
            if (!chatUsersStateCache.IsNonFriendConnected(userid)) return;

            chatUsersStateCache.AddConnectedFriend(userid);
            chatUsersStateCache.RemoveConnectedNonFriend(userid);

            if (openConversations.Contains(userid))
                chatUserStateEventBus.OnFriendConnected(userid);
        }

        private void OnUserUnblocked(string userid)
        {
            if (!chatUsersStateCache.IsBlockedUserConnected(userid)) return;

            chatUsersStateCache.AddConnectedNonFriend(userid);
            chatUsersStateCache.RemovedConnectedBlockedUser(userid);

            if (openConversations.Contains(userid))
                chatUserStateEventBus.OnNonFriendConnected(userid);
        }

        private void OnYouUnblockedProfile(BlockedProfile profile)
        {
            OnUserUnblocked(profile.Address);
        }

        private void OnYouBlockedProfile(BlockedProfile profile)
        {
            var userId = profile.Address;
            if (!chatUsersStateCache.IsUserConnected(userId)) return;

            chatUsersStateCache.RemoveConnectedNonFriend(userId);
            chatUsersStateCache.RemoveConnectedFriend(userId);
            chatUsersStateCache.AddConnectedBlockedUser(userId);

            if (openConversations.Contains(userId))
                chatUserStateEventBus.OnUserBlocked(userId);
        }

        private void OnYouBlockedByUser(string userId)
        {
            if (!chatUsersStateCache.IsUserConnected(userId)) return;

            bool wasFriend = chatUsersStateCache.IsFriendConnected(userId);
            chatUsersStateCache.RemoveConnectedNonFriend(userId);
            chatUsersStateCache.RemoveConnectedFriend(userId);
            chatUsersStateCache.AddConnectedBlockedUser(userId);

            if (!openConversations.Contains(userId)) return;

            if (wasFriend)
                chatUserStateEventBus.OnFriendDisconnected(userId);
            else
                chatUserStateEventBus.OnNonFriendDisconnected(userId);
        }

    }
}
