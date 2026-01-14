using CodeLess.Attributes;
using System.Collections.Generic;

namespace DCL.FeatureFlags
{
    [Singleton]
    public partial class OfficialWalletsHelper
    {
        private readonly HashSet<string> officialWallets = new ();

        public OfficialWalletsHelper()
        {
            if (FeatureFlagsConfiguration.Instance.TryGetCsvPayload(FeatureFlagsStrings.OFFICIAL_WALLETS, FeatureFlagsStrings.WALLETS_VARIANT, out var csv) && csv is { Count: >= 1 })
                foreach (string wallet in csv[0])
                    officialWallets.Add(wallet.ToLowerInvariant());
        }

        public bool IsOfficialWallet(string wallet) =>
            officialWallets.Contains(wallet.ToLowerInvariant());
    }
}
