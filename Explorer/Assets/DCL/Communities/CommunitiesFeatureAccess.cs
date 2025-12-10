using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Web3.Identities;
using Global.AppArgs;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Communities
{
    [Singleton]
    public partial class CommunitiesFeatureAccess
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IAppArgs appArgs;

        private bool? storedResult;
        private bool? storedMembersCounterResult;
        private bool? storedAnnouncementsResult;

        public CommunitiesFeatureAccess(IWeb3IdentityCache web3IdentityCache, IAppArgs appArgs)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.appArgs = appArgs;

            web3IdentityCache.OnIdentityChanged += OnIdentityCacheChanged;
        }

        /// <summary>
        /// Checks if the Communities feature flag is activated and if the user is allowed to use the feature based on the allowlist from the feature flag.
        /// </summary>
        /// <returns>True if the user is allowed to use the feature, false otherwise.</returns>
        public async UniTask<bool> IsUserAllowedToUseTheFeatureAsync(CancellationToken ct, bool ignoreAllowedList = false, bool cacheResult = true)
        {
            //TODO REMOVE THIS!!!! HACK TO ENABLE COMMUNITIES ALL THE TIME
            // P.s. it's safier to put defines here
#if UNITY_EDITOR && !COMMUNITIES_FORCE_USER_WHITELIST
            return true;
#endif

            if (!cacheResult)
                storedResult = null;

            if (storedResult != null)
                return storedResult.Value;

            bool result = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.COMMUNITIES);

            if (result && !ignoreAllowedList)
            {
                await UniTask.WaitUntil(() => web3IdentityCache.Identity != null, cancellationToken: ct);
                var ownWalletId = web3IdentityCache.Identity!.Address;

                if (string.IsNullOrEmpty(ownWalletId))
                    result = false;
                else
                {
                    FeatureFlagsConfiguration.Instance.TryGetTextPayload(FeatureFlagsStrings.COMMUNITIES, FeatureFlagsStrings.WALLETS_VARIANT, out string? walletsAllowlist);
                    result = string.IsNullOrEmpty(walletsAllowlist) || walletsAllowlist.Contains(ownWalletId, StringComparison.OrdinalIgnoreCase);
                }
            }

            storedResult = cacheResult ? result : null;
            return result;
        }

        public bool CanMembersCounterBeDisplayer()
        {
            if (storedMembersCounterResult != null)
                return storedMembersCounterResult.Value;

            bool result = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.COMMUNITIES_MEMBERS_COUNTER);
            storedMembersCounterResult = result;
            return result;
        }

        public bool IsAnnouncementsFeatureEnabled()
        {
            if (storedAnnouncementsResult != null)
                return storedAnnouncementsResult.Value;

            bool result = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.COMMUNITIES_ANNOUNCEMENTS) ||
                          (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.COMMUNITIES_ANNOUNCEMENTS)) ||
                          Application.isEditor;

            storedAnnouncementsResult = result;
            return result;
        }

        public bool GetCommunityIdFromDeepLink(out string? communityId)
        {
            if (appArgs.TryGetValue(AppArgsFlags.COMMUNITY, out communityId) && !string.IsNullOrEmpty(communityId))
                return true;

            communityId = null;
            return false;

        }

        private void OnIdentityCacheChanged() =>
            storedResult = null;
    }
}
