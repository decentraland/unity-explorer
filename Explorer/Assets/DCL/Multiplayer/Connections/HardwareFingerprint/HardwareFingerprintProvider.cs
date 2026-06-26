using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DCL.Multiplayer.Connections.HardwareFingerprint
{
    /// <summary>
    ///     Computes the device fingerprint as a SHA-256 hash of Unity's
    ///     <see cref="SystemInfo.deviceUniqueIdentifier" />. The hash is computed once and cached for the
    ///     lifetime of the instance.
    /// </summary>
    public sealed class HardwareFingerprintProvider : IHardwareFingerprintProvider
    {
        // Versioned domain-separation prefix. Public by design (open-source client) and not a secret:
        // it provides no confidentiality, only a way to rotate the hash format later. Must stay constant
        // across installs so the same machine maps to the same fingerprint regardless of wallet or reinstall.
        private const string DOMAIN_PREFIX = "dcl:explorer:hwfp:v1:";

        public string Fingerprint { get; }

        public HardwareFingerprintProvider()
        {
            Fingerprint = ComputeFingerprint(SystemInfo.deviceUniqueIdentifier);
        }

        /// <summary>
        ///     Hashes a raw device identifier into the wire fingerprint. Returns an empty string when the
        ///     identifier is missing or unsupported, so machines without a stable id are not all collapsed
        ///     onto a single shared hash.
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
