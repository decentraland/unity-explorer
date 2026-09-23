using System;
using System.Security.Cryptography;
using System.Text;

namespace DCL.Prefs
{
    /// <summary>Random id for this installation, generated on first use and persisted; every feature needing a stable anonymous identity derives one from it.</summary>
    public static class AnonymousInstallationId
    {
        // Rotating this rotates every fingerprint; v2 moved off the Unity device id.
        private const string FINGERPRINT_DOMAIN_PREFIX = "dcl:explorer:hwfp:v2:";

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

        /// <summary>Lowercase-hex SHA-256 of the resolved id, so the id itself never leaves the installation.</summary>
        /// <remarks>Replaces <see cref="UnityEngine.SystemInfo.deviceUniqueIdentifier" />, which silently collides between unrelated machines (issue #10199).</remarks>
        public static string ResolveFingerprint()
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(FINGERPRINT_DOMAIN_PREFIX + Resolve()));

            var builder = new StringBuilder(hash.Length * 2);

            foreach (byte hashByte in hash)
                builder.Append(hashByte.ToString("x2"));

            return builder.ToString();
        }
    }
}
