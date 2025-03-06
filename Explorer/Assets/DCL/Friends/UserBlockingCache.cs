using Cysharp.Threading.Tasks;
using DCL.Friends.UserBlocking;
using DCL.Web3.Identities;
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
        private readonly IWeb3IdentityCache web3IdentityCache;

        private readonly HashSet<string> blockedUsers = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> blockedByUsers = new (StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource fetchCts = new ();

        public ReadOnlyHashSet<string> BlockedUsers { get; }
        public ReadOnlyHashSet<string> BlockedByUsers { get; }

        public UserBlockingCache(IFriendsService friendsService,
            IFriendsEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.friendsService = friendsService;
            this.eventBus = eventBus;
            this.web3IdentityCache = web3IdentityCache;

            BlockedUsers = new ReadOnlyHashSet<string>(blockedUsers);
            BlockedByUsers = new ReadOnlyHashSet<string>(blockedByUsers);

            eventBus.OnYouBlockedProfile += UserBlockedByYou;
            eventBus.OnYouUnblockedProfile += UserUnblockedByYou;
            eventBus.OnYouBlockedByUser += YouBlockedByUser;
            eventBus.OnYouUnblockedByUser += YouUnblockedByUser;

            web3IdentityCache.OnIdentityChanged += IdentityChanged;

            IdentityChanged();
        }

        public void Dispose()
        {
            eventBus.OnYouBlockedProfile -= UserBlockedByYou;
            eventBus.OnYouUnblockedProfile -= UserUnblockedByYou;
            eventBus.OnYouBlockedByUser -= YouBlockedByUser;
            eventBus.OnYouUnblockedByUser -= YouUnblockedByUser;

            web3IdentityCache.OnIdentityChanged -= IdentityChanged;

            fetchCts.SafeCancelAndDispose();
        }

        public bool UserIsBlocked(string userId) =>
            BlockedUsers.Contains(userId) || BlockedByUsers.Contains(userId);

        private void IdentityChanged()
        {
            fetchCts = fetchCts.SafeRestart();
            FetchDataAsync(fetchCts.Token).Forget();

            async UniTaskVoid FetchDataAsync(CancellationToken ct)
            {
                UserBlockingStatus blockingStatus = await friendsService.GetUserBlockingStatusAsync(ct);
                blockedUsers.Clear();
                blockedByUsers.Clear();

                foreach (string user in blockingStatus.BlockedUsers)
                    blockedUsers.Add(user.ToLower());
                foreach (string user in blockingStatus.BlockedByUsers)
                    blockedByUsers.Add(user.ToLower());
            }
        }

        private void UserBlockedByYou(BlockedProfile user) => blockedUsers.Add(user.Address);
        private void UserUnblockedByYou(BlockedProfile user) => blockedUsers.Remove(user.Address);
        private void YouBlockedByUser(string userAddress) => blockedByUsers.Add(userAddress);
        private void YouUnblockedByUser(string userAddress) => blockedByUsers.Remove(userAddress);
    }
}
