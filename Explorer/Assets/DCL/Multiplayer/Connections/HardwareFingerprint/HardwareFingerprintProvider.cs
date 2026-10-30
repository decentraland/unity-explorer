using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DCL.Multiplayer.Connections.HardwareFingerprint
{
    /// <summary>
    ///     SHA-256 hash of <see cref="SystemInfo.deviceUniqueIdentifier" />, computed once at construction.
    /// </summary>
    public sealed class HardwareFingerprintProvider
    {
        // Versioned prefix so the hash format can be rotated later. Not a secret; must stay constant
        // so the same machine maps to the same fingerprint across wallets and reinstalls.
        private const string DOMAIN_PREFIX = "dcl:explorer:hwfp:v1:";

        public string Fingerprint { get; }

        public HardwareFingerprintProvider()
        {
            Fingerprint = ComputeFingerprint(SystemInfo.deviceUniqueIdentifier);
        }

        /// <summary>
        ///     Returns empty for a missing/unsupported id so those machines aren't collapsed onto one hash.
        /// </summary>
        public static string ComputeFingerprint(string rawDeviceId)
        {
            if (string.IsNullOrEmpty(rawDeviceId) || rawDeviceId == SystemInfo.unsupportedIdentifier)
                return string.Empty;

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(DOMAIN_PREFIX + rawDeviceId.Trim().ToLowerInvariant()));

            var builder = new StringBuilder(hash.Length * 2);

            foreach (byte hashByte in hash)
                builder.Append(hashByte.ToString("x2"));

            return builder.ToString();
        }
    }
}
