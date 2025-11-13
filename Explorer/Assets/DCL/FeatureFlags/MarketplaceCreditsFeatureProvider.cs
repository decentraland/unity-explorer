using Cysharp.Threading.Tasks;
using DCL.Profiles.Self;
using ECS;
using System;
using System.Threading;

namespace DCL.FeatureFlags
{
    public class MarketplaceCreditsFeatureProvider : IFeatureProvider
    {
        private readonly IRealmData realmData;
        private readonly ISelfProfile selfProfile;
        private bool? storedResult;

        public MarketplaceCreditsFeatureProvider(IRealmData realmData, ISelfProfile selfProfile)
        {
            this.realmData = realmData;
            this.selfProfile = selfProfile;
        }

        public async UniTask<bool> IsFeatureEnabledAsync(CancellationToken ct)
        {
            if (storedResult != null)
                return storedResult.Value;

            bool baseFeatureState = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS);

            if (!baseFeatureState)
            {
                storedResult = false;
                return false;
            }

            await UniTask.WaitUntil(() => realmData.Configured, cancellationToken: ct);
            var ownProfile = await selfProfile.ProfileAsync(ct);

            if (ownProfile == null)
            {
                storedResult = false;
                return false;
            }

            if (string.IsNullOrEmpty(ownProfile.UserId))
            {
                storedResult = false;
                return false;
            }

            FeatureFlagsConfiguration.Instance.TryGetTextPayload(FeatureFlagsStrings.MARKETPLACE_CREDITS, FeatureFlagsStrings.WALLETS_VARIANT, out string? walletsForTestingMarketplaceCredits);
            bool result = walletsForTestingMarketplaceCredits == null || walletsForTestingMarketplaceCredits.Contains(ownProfile.UserId, StringComparison.OrdinalIgnoreCase);

            storedResult = result;
            return result;
        }
    }
}

