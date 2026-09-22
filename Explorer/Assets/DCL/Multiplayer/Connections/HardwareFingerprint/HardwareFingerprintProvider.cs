using DCL.Prefs;
using System.Security.Cryptography;
using System.Text;

namespace DCL.Multiplayer.Connections.HardwareFingerprint
{
    /// <summary>
    ///     SHA-256 hash of the <see cref="AnonymousInstallationId" />, computed once at construction.
    ///     <para>
    ///         That id stays the same across wallets and sessions on one installation and is never shared with
    ///         another one. It replaces
    ///         <see cref="UnityEngine.SystemInfo.deviceUniqueIdentifier" />, which collides between unrelated Windows
    ///         machines whose firmware reports placeholder serials and between clones of a VM image, collapsing them
    ///         onto a single moderation identity.
    ///     </para>
    /// </summary>
    public sealed class HardwareFingerprintProvider
    {
        // Versioned prefix so the hash format can be rotated later. Not a secret; must stay constant
        // so the same installation maps to the same fingerprint. v2 moved off the Unity device id.
        private const string DOMAIN_PREFIX = "dcl:explorer:hwfp:v2:";

        public string Fingerprint { get; }

        public HardwareFingerprintProvider()
        {
            Fingerprint = ComputeFingerprint(AnonymousInstallationId.Resolve());
        }

        /// <summary>
        ///     Hashing keeps the installation id itself out of the gatekeeper payload, and the domain prefix keeps
        ///     this value distinct from anything else derived from that id.
        /// </summary>
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
