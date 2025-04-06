using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Utilities;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Chat
{
    public class ChatUserStateUpdater
    {
        //Here have all events for user connected/disconnected (blocked/unblocked us), blocked/unblocked by us, changed settings on/off, we changed settings
        //Use ChatUserStateEventBus?


        private readonly IChatUsersStateCache chatUsersStateCache;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ObjectProxy<FriendsCache> friendsCacheProxy;
        private readonly IParticipantsHub participantsHub;
        /// <summary>
        /// We will use this to track which conversations are open and decide if its necessary to notify the controller about changes
        /// </summary>
        private readonly HashSet<string> openConversations = new ();

        public ChatUserStateUpdater(
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            ObjectProxy<FriendsCache> friendsCacheProxy,
            IParticipantsHub participantsHub)
        {
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.friendsCacheProxy = friendsCacheProxy;
            this.participantsHub = participantsHub;
            participantsHub.UpdatesFromParticipant += OnUpdatesFromParticipant;
        }

        public void Initialize(IEnumerable<string> openConversations)
        {
            foreach (string conversation in openConversations)
                this.openConversations.Add(conversation);


            if (!friendsCacheProxy.Configured) return;
            if (!userBlockingCacheProxy.Configured) return;

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
                        //Notify Controller? or add to list that we return to controller directly, to avoid many single notifications?
                    }

                    //Add to temp list to request updated privacy state of these users
                }

                //When this finishes, we have a proper list of all connected users, so we can setup the conversations sidebar UI
                //If we can publish in the metada the info on their settings, we can also know if they allow non-friends connections as well

                //To update our metadata we can use Room.UpdateLocalMetadata -> we probably need to do this when switching our settings and when first reading them.
                //Then the other clients receive a notification of metadata updated and update the cache accordingly.
            }
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
                            //notify controller
                        }
                    }
                    else if (!userBlockingCacheProxy.StrictObject.UserIsBlocked(participant.Identity))
                    {
                        chatUsersStateCache.AddConnectedNonFriend(participant.Identity);
                        if (openConversations.Contains(participant.Identity))
                        {
                            //notify controller
                        }
                    }
                    break;
                case UpdateFromParticipant.MetadataChanged:
                    //Parse metadata and if its not a friend and its not blocked and its set as not allowing, add to the list
                    break;
                case UpdateFromParticipant.Disconnected:
                    if (friendsCacheProxy.StrictObject.Contains(participant.Identity))
                        chatUsersStateCache.RemoveConnectedFriend(participant.Identity);
                    else
                        chatUsersStateCache.RemoveConnectedNonFriend(participant.Identity);

                    if (openConversations.Contains(participant.Identity))
                    {
                        //notify controller
                    }
                    break;
            }

        }

    }
}
