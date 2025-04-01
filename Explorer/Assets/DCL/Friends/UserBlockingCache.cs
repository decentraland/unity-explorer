using DCL.Friends.UserBlocking;
using System;
using System.Collections.Generic;

namespace DCL.Friends
{
    public class UserBlockingCache : IUserBlockingCache, IDisposable
    {
        private readonly IFriendsEventBus eventBus;

        private readonly HashSet<string> blockedUsers = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> blockedByUsers = new (StringComparer.OrdinalIgnoreCase);

        public ReadOnlyHashSet<string> BlockedUsers { get; }
        public ReadOnlyHashSet<string> BlockedByUsers { get; }

        // This property is fixed to true since we currently don't want to show chat messages from blocked users in any shape or form.
        // This can change in the future, so all the logic that is already in place will stay the same for such times.
        public bool HideChatMessages
        {
            get => true;

            set { }
        }

        public UserBlockingCache(IFriendsEventBus eventBus)
        {
            this.eventBus = eventBus;

            BlockedUsers = new ReadOnlyHashSet<string>(blockedUsers);
            BlockedByUsers = new ReadOnlyHashSet<string>(blockedByUsers);

            eventBus.OnYouBlockedProfile += UserBlockedByYou;
            eventBus.OnYouUnblockedProfile += UserUnblockedByYou;
            eventBus.OnYouBlockedByUser += YouBlockedByUser;
            eventBus.OnYouUnblockedByUser += YouUnblockedByUser;
        }

        public void Dispose()
        {
            eventBus.OnYouBlockedProfile -= UserBlockedByYou;
            eventBus.OnYouUnblockedProfile -= UserUnblockedByYou;
            eventBus.OnYouBlockedByUser -= YouBlockedByUser;
            eventBus.OnYouUnblockedByUser -= YouUnblockedByUser;
        }

        public bool UserIsBlocked(string userId) =>
            BlockedUsers.Contains(userId) || BlockedByUsers.Contains(userId);

        public void Reset(UserBlockingStatus blockingStatus)
        {
            blockedUsers.Clear();
            blockedByUsers.Clear();

            foreach (string user in blockingStatus.BlockedUsers)
                blockedUsers.Add(user);
            foreach (string user in blockingStatus.BlockedByUsers)
                blockedByUsers.Add(user);
        }

        private void UserBlockedByYou(BlockedProfile user) => blockedUsers.Add(user.Address);
        private void UserUnblockedByYou(BlockedProfile user) => blockedUsers.Remove(user.Address);
        private void YouBlockedByUser(string userAddress) => blockedByUsers.Add(userAddress);
        private void YouUnblockedByUser(string userAddress) => blockedByUsers.Remove(userAddress);
    }
}
