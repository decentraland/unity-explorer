using DCL.Prefs;
using System.Security.Cryptography;
using System.Text;

namespace DCL.Multiplayer.Connections.HardwareFingerprint
{
    /// <summary>SHA-256 hash of the <see cref="AnonymousInstallationId" />, computed once at construction.</summary>
    /// <remarks>Replaces <see cref="UnityEngine.SystemInfo.deviceUniqueIdentifier" />, which silently collides between unrelated machines (issue #10199).</remarks>
    public sealed class HardwareFingerprintProvider
    {
        // Rotating this rotates every fingerprint; v2 moved off the Unity device id.
        private const string DOMAIN_PREFIX = "dcl:explorer:hwfp:v2:";

        public string Fingerprint { get; }

        public HardwareFingerprintProvider()
        {
            Fingerprint = ComputeFingerprint(AnonymousInstallationId.Resolve());
        }

        private static string ComputeFingerprint(string installationId)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(DOMAIN_PREFIX + installationId));

            var builder = new StringBuilder(hash.Length * 2);

            foreach (byte hashByte in hash)
                builder.Append(hashByte.ToString("x2"));

            return builder.ToString();
        }
    }
}
