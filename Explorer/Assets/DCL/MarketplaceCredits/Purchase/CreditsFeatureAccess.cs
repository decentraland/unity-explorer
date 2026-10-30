using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Gates the credits feature set (credits panel, top-up, buy with credits) behind the
    ///     <see cref="FeatureFlagsStrings.CREDITS_WALLETS" /> restriction flag: when the flag is enabled,
    ///     only wallets listed in its "wallets" variant payload are allowed; when the flag is disabled or
    ///     the payload is empty, everyone is allowed.
    /// </summary>
    [Singleton]
    public partial class CreditsFeatureAccess
    {
        private readonly IWeb3IdentityCache web3IdentityCache;

        private bool? storedResult;

        public CreditsFeatureAccess(IWeb3IdentityCache web3IdentityCache, CancellationToken warmUpCt)
        {
            this.web3IdentityCache = web3IdentityCache;

            web3IdentityCache.OnIdentityChanged += OnIdentityCacheChanged;

            // Resolve the verdict in the background so the cached result is ready as soon as the
            // identity resolves, without depending on a caller running the async check first.
            IsUserAllowedToUseTheFeatureAsync(warmUpCt).SuppressToResultAsync(ReportCategory.CREDITS_PURCHASE).Forget();
        }

        /// <summary>
        ///     Synchronous allowlist verdict: returns the cached result when available, otherwise computes it
        ///     from the local feature-flag payload. While no identity is set it returns false WITHOUT caching,
        ///     so the first call with an identity computes the real verdict.
        /// </summary>
        public bool IsUserAllowed()
        {
            if (storedResult != null)
                return storedResult.Value;

            IWeb3Identity? identity = web3IdentityCache.Identity;

            if (identity == null)
                return false;

            storedResult = ComputeIsUserAllowed(identity.Address);
            return storedResult.Value;
        }

        /// <summary>
        ///     Counterpart of <see cref="IsUserAllowed" /> for callers that may run before login: awaits an
        ///     identity before resolving and caching the verdict.
        /// </summary>
        public async UniTask<bool> IsUserAllowedToUseTheFeatureAsync(CancellationToken ct)
        {
            if (storedResult != null)
                return storedResult.Value;

            await UniTask.WaitUntil(() => web3IdentityCache.Identity != null, cancellationToken: ct);

            return IsUserAllowed();
        }

        private static bool ComputeIsUserAllowed(string wallet)
        {
            // Restriction flag disabled -> no restriction, everyone is allowed
            if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CREDITS_WALLETS))
                return true;

            if (string.IsNullOrEmpty(wallet))
                return false;

            FeatureFlagsConfiguration.Instance.TryGetTextPayload(FeatureFlagsStrings.CREDITS_WALLETS, FeatureFlagsStrings.WALLETS_VARIANT, out string? walletsAllowlist);

            return string.IsNullOrEmpty(walletsAllowlist) || walletsAllowlist.Contains(wallet, StringComparison.OrdinalIgnoreCase);
        }

        private void OnIdentityCacheChanged() =>
            storedResult = null;
    }
}
