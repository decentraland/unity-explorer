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

        public bool IsEmpty => flags.Count == 0 && variants.Count == 0;

        // Newtonsoft-deserialized wire DTO (CreateFromJson with WRJsonParser.Newtonsoft); Unity serialization never sees these fields.
#pragma warning disable UAC1009
        public Dictionary<string, bool> flags;
        public Dictionary<string, FeatureFlagVariantDto> variants;
#pragma warning restore UAC1009
    }
}
