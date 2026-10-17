using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using Global.AppArgs;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DCL.Communities
{
    [Singleton]
    public partial class CommunitiesFeatureAccess
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IAppArgs appArgs;

        private bool? storedResult;

#if UNITY_EDITOR
        // Lets Editor tests exercise the real gating that <see cref="forceEnabledInEditor" /> otherwise short-circuits.
        internal static bool disableForTests { get; set; }
#endif

        private static bool forceEnabledInEditor
        {
            //TODO REMOVE THIS!!!! HACK TO ENABLE COMMUNITIES ALL THE TIME
            // Compile-time so the hack cannot leak into player builds; define COMMUNITIES_FORCE_USER_WHITELIST
            // in the Editor to exercise the real feature-flag + allowlist gating.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if UNITY_EDITOR && !COMMUNITIES_FORCE_USER_WHITELIST
                return !disableForTests;
#else
                return false;
#endif
            }
        }

        public CommunitiesFeatureAccess(IWeb3IdentityCache web3IdentityCache, IAppArgs appArgs, CancellationToken warmUpCt)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.appArgs = appArgs;

            web3IdentityCache.OnIdentityChanged += OnIdentityCacheChanged;

            // Resolve the allowlist-aware check in the background so the cached result is available
            // without depending on another feature flow having run the check first.
            IsUserAllowedToUseTheFeatureAsync(warmUpCt).SuppressToResultAsync(ReportCategory.COMMUNITIES).Forget();
        }

        /// <summary>
        ///     Checks only the Communities feature flag, ignoring the wallets allowlist. Synchronous and
        ///     identity-independent, so it is safe to call from bootstrap paths that run before login.
        /// </summary>
        public bool IsFeatureEnabled() =>
            forceEnabledInEditor || FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.COMMUNITIES);

        /// <summary>
        ///     Checks if the Communities feature flag is activated and if the user is allowed to use the feature
        ///     based on the wallets allowlist from the feature flag. The verdict is cached until the identity
        ///     changes (see <see cref="IsUserAllowedCached" />).
        /// </summary>
        /// <returns>True if the user is allowed to use the feature, false otherwise.</returns>
        public async UniTask<bool> IsUserAllowedToUseTheFeatureAsync(CancellationToken ct)
        {
            if (forceEnabledInEditor)
                return true;

            if (storedResult != null)
                return storedResult.Value;

            bool result = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.COMMUNITIES);

            if (result)
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

            storedResult = result;
            return result;
        }

        /// <summary>
        ///     Synchronous counterpart of <see cref="IsUserAllowedToUseTheFeatureAsync" />: returns the cached
        ///     result of the last completed check, or false while no check has resolved for the current identity.
        /// </summary>
        public bool IsUserAllowedCached() =>
            forceEnabledInEditor || (storedResult ?? false);

        public bool TryGetCommunityIdFromAppArgs(out string? communityId) =>
            appArgs.TryGetValue(AppArgsFlags.COMMUNITY, out communityId) && !string.IsNullOrEmpty(communityId);

        private void OnIdentityCacheChanged() =>
            storedResult = null;
    }
}
