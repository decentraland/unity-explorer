using System;
using System.Collections.Generic;

namespace DCL.FeatureFlags
{
    /// <summary>
    ///     Reads the deep-link world whitelist flag. Read early in bootstrap from <see cref="StartupFeatureFlagsFetcher" />,
    ///     before the cold-start deep-link parse, so the deep-link allowlist can trust configured worlds at launch time.
    /// </summary>
    public static class DeepLinkWorldWhitelistProvider
    {
        private static readonly char[] SEPARATORS = { ',', '\n', '\r', ';' };

        /// <summary>
        ///     Reads the whitelisted worlds from a configuration whose keys are already app-name-stripped.
        /// </summary>
        public static IReadOnlyList<string> ReadWorlds(FeatureFlagsConfiguration configuration)
        {
            if (!configuration.TryGetPayload(FeatureFlagsStrings.DEEPLINK_WHITELISTED_WORLDS, out FeatureFlagPayload payload)
                || string.IsNullOrWhiteSpace(payload.value))
                return Array.Empty<string>();

            var worlds = new List<string>();

            // The payload lists world names (full or short form) separated by commas or newlines. Normalization to
            // the canonical world name is done by DeepLinkAllowlist, so entries are passed through verbatim here.
            foreach (string entry in payload.value.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = entry.Trim();

                if (trimmed.Length > 0)
                    worlds.Add(trimmed);
            }

            return worlds;
        }
    }
}
