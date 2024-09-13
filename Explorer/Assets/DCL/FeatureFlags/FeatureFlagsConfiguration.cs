using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace DCL.FeatureFlags
{
    public enum FeatureFlags
    {
        PORTABLE_EXPERIENCE,
        GLOBAL_PORTABLE_EXPERIENCE,
        PORTABLE_EXPERIENCE_CHAT_COMMANDS,
        MAP_PINS,
        CUSTOM_MAP_PINS_ICONS
    }

    public class FeatureFlagsConfiguration
    {
        public static string GetFlag(FeatureFlags flags)
        {
            switch (flags)
            {
                case FeatureFlags.PORTABLE_EXPERIENCE:
                    return "alfa-portable-experiences";
                case FeatureFlags.GLOBAL_PORTABLE_EXPERIENCE:
                    return "alfa-global-portable-experiences";
                case FeatureFlags.PORTABLE_EXPERIENCE_CHAT_COMMANDS:
                    return "alfa-portable-experiences-chat-commands";
                case FeatureFlags.MAP_PINS:
                    return "alfa-map-pins";
                case FeatureFlags.CUSTOM_MAP_PINS_ICONS:
                    return "alfa-map-pins-custom-icons";
                default:
                    return flags.ToString();
            }
        }

        private readonly FeatureFlagsResultDto result;

        public FeatureFlagsConfiguration(FeatureFlagsResultDto result)
        {
            this.result = result;
        }

        public bool IsEnabled(string id) =>
            result.flags.GetValueOrDefault(id, false);

        public bool IsEnabled(string id, string variantId)
        {
            if (!result.variants.TryGetValue(id, out FeatureFlagVariantDto variant)) return false;
            return variant.name == variantId == variant.enabled;
        }

        public bool TryGetJsonPayload<T>(string id, string variantId, out T? json)
        {
            json = default(T);

            if (!TryGetPayload(id, variantId, out FeatureFlagPayload payload)) return false;
            if (!string.Equals(payload.type, "json", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(payload.value)) return false;

            json = JsonConvert.DeserializeObject<T>(payload.value);

            return true;
        }

        public bool TryGetCsvPayload(string id, string variantId, out List<List<string>>? csv)
        {
            csv = null;

            if (!TryGetPayload(id, variantId, out FeatureFlagPayload payload)) return false;
            if (!string.Equals(payload.type, "csv", StringComparison.OrdinalIgnoreCase)) return false;
            string str = payload.value;
            if (string.IsNullOrEmpty(str)) return false;

            csv = new List<List<string>>();
            using StringReader reader = new (str);

            string? line;

            while ((line = reader.ReadLine()) != null)
                csv.Add(new List<string>(line.Split(',', StringSplitOptions.RemoveEmptyEntries)));

            return true;
        }

        public bool TryGetTextPayload(string id, string variantId, out string? text)
        {
            text = null;

            if (!TryGetPayload(id, variantId, out FeatureFlagPayload payload)) return false;
            if (!string.Equals(payload.type, "string", StringComparison.OrdinalIgnoreCase)) return false;

            text = payload.value;

            return true;
        }

        public bool TryGetPayload(string id, out FeatureFlagPayload payload)
        {
            payload = default(FeatureFlagPayload);

            if (!result.variants.TryGetValue(id, out FeatureFlagVariantDto variant)) return false;

            payload = variant.payload;

            return true;
        }

        public bool TryGetPayload(string id, string variantId, out FeatureFlagPayload payload)
        {
            payload = default(FeatureFlagPayload);

            if (!result.variants.TryGetValue(id, out FeatureFlagVariantDto variant)) return false;
            if (variant.name != variantId) return false;

            payload = variant.payload;

            return true;
        }
    }
}
