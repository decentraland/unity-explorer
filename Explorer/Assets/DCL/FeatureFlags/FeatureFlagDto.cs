using System;
using System.Collections.Generic;

namespace DCL.FeatureFlags
{
    [Serializable]
    public struct FeatureFlagVariantDto
    {
        public string name;
        public bool enabled;
        public FeatureFlagPayload payload;
    }

    [Serializable]
    public struct FeatureFlagPayload
    {
        public string type;
        public string value;
    }

    [Serializable]
    public struct FeatureFlagsResultDto
    {
        public static FeatureFlagsResultDto Empty => new()
        {
            flags = new Dictionary<string, bool>(),
            variants = new Dictionary<string, FeatureFlagVariantDto>(),
        };

        public Dictionary<string, bool> flags;
        public Dictionary<string, FeatureFlagVariantDto> variants;
    }
}
