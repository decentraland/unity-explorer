using System;

namespace DCL.Prefs
{
    /// <summary>
    ///     Random id identifying this installation, generated on first use and persisted.
    ///     <para>
    ///         It carries no relation to a wallet, a machine or a person: it stays the same across wallets and
    ///         sessions, and a reinstall or a cleared preferences store yields a new one. Features that need a
    ///         stable anonymous identity derive theirs from it rather than persisting one of their own, so a
    ///         single value can be reasoned about and reset.
    ///     </para>
    /// </summary>
    public static class AnonymousInstallationId
    {
        public static string Resolve()
        {
            string persisted = DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID);

            if (!string.IsNullOrWhiteSpace(persisted))
                return persisted;

            string legacy = DCLPlayerPrefs.GetString(DCLPrefKeys.LEGACY_FEATURE_FLAGS_USER_ID);
            string resolved = string.IsNullOrWhiteSpace(legacy) ? Guid.NewGuid().ToString() : legacy;

            DCLPlayerPrefs.SetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID, resolved, true);

            return resolved;
        }
    }
}
