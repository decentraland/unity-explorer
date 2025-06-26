using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Profiles.Self;
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
        public static async UniTask<bool> IsUserAllowedToUseTheFeatureAsync(IRealmData realmData, ISelfProfile selfProfile, FeatureFlagsCache featureFlagsCache, CancellationToken ct)
        {
            bool userAllowed = featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.COMMUNITIES);

            if (userAllowed)
            {
                await UniTask.WaitUntil(() => realmData.Configured, cancellationToken: ct);
                var ownProfile = await selfProfile.ProfileAsync(ct);
                if (ownProfile != null && !string.IsNullOrEmpty(ownProfile.UserId))
                {
                    featureFlagsCache.Configuration.TryGetTextPayload(FeatureFlagsStrings.COMMUNITIES, FeatureFlagsStrings.COMMUNITIES_WALLETS_VARIANT, out string walletsAllowlist);
                    userAllowed = string.IsNullOrEmpty(walletsAllowlist) || walletsAllowlist.Contains(ownProfile.UserId, StringComparison.OrdinalIgnoreCase);
                }
            }

            return userAllowed;
        }
    }
}
