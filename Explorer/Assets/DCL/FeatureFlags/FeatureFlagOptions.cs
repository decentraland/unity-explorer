using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using System;

namespace DCL.FeatureFlags
{
    public struct FeatureFlagOptions
    {
        public Web3Address? UserId { get; set; }
        public bool Debug { get; set; }
        public URLDomain URL { get; set; }
        /// <summary>
        /// Refers to the application concept: https://docs.getunleash.io/reference/applications
        /// ie: explorer, dao, dapps, places, etc..
        /// </summary>
        public string AppName { get; set; }
        /// <summary>
        /// Applies for application hostname strategy: https://gh.getunleash.io/reference/activation-strategies#hostnames
        /// ie: decentraland.org, decentraland.zone, localhost
        /// </summary>
        public string Hostname { get; set; }

        public static FeatureFlagOptions NewFeatureFlagOptions(IDecentralandUrlsSource decentralandUrlsSource) =>
            new()
            {
                AppName = "explorer",
                URL = URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.FeatureFlags)),
                Debug = false,
                Hostname = decentralandUrlsSource.Url(DecentralandUrl.Host),
            };
    }
}
