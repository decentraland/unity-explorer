using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.Communities
{
    public class CommunitiesFeatureAccess : IDisposable
    {
        private readonly IWeb3IdentityCache web3IdentityCache;

        private bool? storedResult;

        public CommunitiesFeatureAccess(IWeb3IdentityCache web3IdentityCache)
        {
            this.web3IdentityCache = web3IdentityCache;

            web3IdentityCache.OnIdentityChanged += OnIdentityCacheChanged;
        }

        public void Dispose() =>
            web3IdentityCache.OnIdentityChanged -= OnIdentityCacheChanged;

        /// <summary>
        /// Checks if the Communities feature flag is activated and if the user is allowed to use the feature based on the allowlist from the feature flag.
        /// </summary>
        /// <returns>True if the user is allowed to use the feature, false otherwise.</returns>
        public async UniTask<bool> IsUserAllowedToUseTheFeatureAsync(CancellationToken ct, bool ignoreAllowedList = false, bool cacheResult = true)
        {
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
                    FeatureFlagsConfiguration.Instance.TryGetTextPayload(FeatureFlagsStrings.COMMUNITIES, FeatureFlagsStrings.COMMUNITIES_WALLETS_VARIANT, out string walletsAllowlist);
                    result = string.IsNullOrEmpty(walletsAllowlist) || walletsAllowlist.Contains(ownWalletId, StringComparison.OrdinalIgnoreCase);
                }
            }

            storedResult = cacheResult ? result : null;
            return result;
        }

        private void OnIdentityCacheChanged() =>
            storedResult = null;
    }
}
