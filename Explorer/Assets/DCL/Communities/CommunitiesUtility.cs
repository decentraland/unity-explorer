using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using ECS;
using System;
using System.Threading;

namespace DCL.Communities
{
    public static class CommunitiesUtility
    {
        private static ReadOnlySpan<char> formatSpan => "0.#".AsSpan();

        public static string NumberToCompactString(long number)
        {
            int charsWritten = 0;
            Span<char> destination = stackalloc char[16];

            switch (number)
            {
                case >= 1_000_000_000:
                {
                    double value = number / 1_000_000_000D;
                    if (value.TryFormat(destination, out int written, formatSpan))
                    {
                        destination[written++] = 'B';
                        charsWritten = written;
                    }

                    break;
                }
                case >= 1_000_000:
                {
                    double value = number / 1_000_000D;
                    if (value.TryFormat(destination, out int written, formatSpan))
                    {
                        destination[written++] = 'M';
                        charsWritten = written;
                    }

                    break;
                }
                case >= 1_000:
                {
                    double value = number / 1_000D;
                    if (value.TryFormat(destination, out int written, formatSpan))
                    {
                        destination[written++] = 'k';
                        charsWritten = written;
                    }

                    break;
                }
                default:
                {
                    if (number.TryFormat(destination, out int written))
                    {
                        charsWritten = written;
                    }

                    break;
                }
            }

            return new string(destination.Slice(0, charsWritten));
        }

        /// <summary>
        /// Checks if the Communities feature flag is activated and if the user is allowed to use the feature based on the allowlist from the feature flag.
        /// </summary>
        /// <returns>True if the user is allowed to use the feature, false otherwise.</returns>
        public static async UniTask<bool> IsUserAllowedToUseTheFeatureAsync(IWeb3IdentityCache web3IdentityCache, FeatureFlagsCache featureFlagsCache, CancellationToken ct)
        {
            if (!featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.COMMUNITIES))
                return false;

            if (web3IdentityCache == null)
                return true;

            await UniTask.WaitUntil(() => web3IdentityCache.Identity != null, cancellationToken: ct);
            var ownWalletId = web3IdentityCache.Identity!.Address;

            if (string.IsNullOrEmpty(ownWalletId))
                return false;

            if (featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.COMMUNITIES, FeatureFlagsStrings.COMMUNITIES_WALLETS_VARIANT, out string walletsAllowlist))
                return string.IsNullOrEmpty(walletsAllowlist) || walletsAllowlist.Contains(ownWalletId, StringComparison.OrdinalIgnoreCase);

            return true;
        }
    }
}
