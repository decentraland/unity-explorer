using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public static class GuestSessionIdProvider
    {
        private const string DOMAIN_PREFIX = "dcl:explorer:guest:v1:";

        public static string? Resolve(string? overrideId) =>
            Resolve(overrideId, SystemInfo.deviceUniqueIdentifier);

        internal static string? Resolve(string? overrideId, string rawDeviceId)
        {
            if (string.IsNullOrEmpty(rawDeviceId) || rawDeviceId == SystemInfo.unsupportedIdentifier)
                return overrideId;

            // The override extends the device rather than replacing it, so the same override on two
            // machines resolves two accounts: sharing one guest account across devices puts both clients on
            // the same address, and a single comms room only keeps one of them.
            string seed = string.IsNullOrEmpty(overrideId)
                ? rawDeviceId.Trim().ToLowerInvariant()
                : $"{rawDeviceId.Trim().ToLowerInvariant()}:{overrideId}";

            // Only the digest is returned, never the raw device id, and the domain prefix scopes it to
            // guest login so it stays independent from any other value derived from the same hardware.
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(DOMAIN_PREFIX + seed));

            var builder = new StringBuilder(hash.Length * 2);

            foreach (byte hashByte in hash)
                builder.Append(hashByte.ToString("x2"));

            return builder.ToString();
        }
    }
}
