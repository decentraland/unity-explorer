using CommunicationData.URLHelpers;
using DCL.Web3;

namespace DCL.FeatureFlags
{
    public struct FeatureFlagOptions
    {
        public Web3Address? UserId { get; set; }
        public bool Debug { get; set; }
        public URLDomain URL { get; set; }
        public string AppName { get; set; }

        public static FeatureFlagOptions Default = new()
        {
            AppName = "explorer",
            URL = URLDomain.FromString("https://feature-flags.decentraland.org"),
            Debug = false,
        };
    }
}
