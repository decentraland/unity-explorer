using System;

namespace DCL.Prefs
{
    /// <summary>Random id for this installation, generated on first use and persisted; every feature needing a stable anonymous identity derives one from it.</summary>
    public static class AnonymousInstallationId
    {
        public static string Resolve()
        {
            string persisted = DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID);

            if (!string.IsNullOrWhiteSpace(persisted))
                return persisted;

            // Adopting the pre-existing id keeps an installation in the A/B bucket it was already in.
            string legacy = DCLPlayerPrefs.GetString(DCLPrefKeys.LEGACY_FEATURE_FLAGS_USER_ID);
            string resolved = string.IsNullOrWhiteSpace(legacy) ? Guid.NewGuid().ToString() : legacy;

            DCLPlayerPrefs.SetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID, resolved, true);

            return resolved;
        }
    }
}
