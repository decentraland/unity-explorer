using Cysharp.Threading.Tasks;
using DCL.Friends.UserBlocking;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Friends
{
    public class UserBlockingCache : IUserBlockingCache, IDisposable
    {
        private readonly IFriendsService friendsService;
        private readonly IFriendsEventBus eventBus;

        private readonly HashSet<string> blockedUsers = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> blockedByUsers = new (StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource fetchCts = new ();

        public ReadOnlyHashSet<string> BlockedUsers { get; }
        public ReadOnlyHashSet<string> BlockedByUsers { get; }
        public bool HideChatMessages { get; set; }

        public UserBlockingCache(IFriendsService friendsService,
            IFriendsEventBus eventBus)
        {
            this.friendsService = friendsService;
            this.eventBus = eventBus;

            BlockedUsers = new ReadOnlyHashSet<string>(blockedUsers);
            BlockedByUsers = new ReadOnlyHashSet<string>(blockedByUsers);

            eventBus.OnYouBlockedProfile += UserBlockedByYou;
            eventBus.OnYouUnblockedProfile += UserUnblockedByYou;
            eventBus.OnYouBlockedByUser += YouBlockedByUser;
            eventBus.OnYouUnblockedByUser += YouUnblockedByUser;

            ResetCache();
        }

        public void Dispose()
        {
            eventBus.OnYouBlockedProfile -= UserBlockedByYou;
            eventBus.OnYouUnblockedProfile -= UserUnblockedByYou;
            eventBus.OnYouBlockedByUser -= YouBlockedByUser;
            eventBus.OnYouUnblockedByUser -= YouUnblockedByUser;

            fetchCts.SafeCancelAndDispose();
        }

        public bool UserIsBlocked(string userId) =>
            BlockedUsers.Contains(userId) || BlockedByUsers.Contains(userId);

        public void ResetCache()
        {
            fetchCts = fetchCts.SafeRestart();
            FetchDataAsync(fetchCts.Token).Forget();

            async UniTaskVoid FetchDataAsync(CancellationToken ct)
            {
                UserBlockingStatus blockingStatus = await friendsService.GetUserBlockingStatusAsync(ct);
                blockedUsers.Clear();
                blockedByUsers.Clear();

                foreach (string user in blockingStatus.BlockedUsers)
                    blockedUsers.Add(user);
                foreach (string user in blockingStatus.BlockedByUsers)
                    blockedByUsers.Add(user);
            }
        }

        private void UserBlockedByYou(BlockedProfile user) => blockedUsers.Add(user.Address);
        private void UserUnblockedByYou(BlockedProfile user) => blockedUsers.Remove(user.Address);
        private void YouBlockedByUser(string userAddress) => blockedByUsers.Add(userAddress);
        private void YouUnblockedByUser(string userAddress) => blockedByUsers.Remove(userAddress);
    }
}
