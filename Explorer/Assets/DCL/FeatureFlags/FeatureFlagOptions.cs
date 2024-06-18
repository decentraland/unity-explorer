using CommunicationData.URLHelpers;
using DCL.Web3;

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

        public static FeatureFlagOptions ORG = new()
        {
            AppName = "explorer",
            URL = URLDomain.FromString("https://feature-flags.decentraland.org"),
            Debug = false,
            Hostname = "https://decentraland.org",
        };

        public static FeatureFlagOptions ZONE = new()
        {
            AppName = "explorer",
            URL = URLDomain.FromString("https://feature-flags.decentraland.zone"),
            Debug = false,
            Hostname = "https://decentraland.zone",
        };
    }
}
