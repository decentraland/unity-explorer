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
            if (!string.IsNullOrEmpty(overrideId))
                return overrideId;

            if (string.IsNullOrEmpty(rawDeviceId) || rawDeviceId == SystemInfo.unsupportedIdentifier)
                return null;

            // Only the digest is returned, never the raw device id, and the domain prefix scopes it to
            // guest login so it stays independent from any other value derived from the same hardware.
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(DOMAIN_PREFIX + rawDeviceId.Trim().ToLowerInvariant()));

            var builder = new StringBuilder(hash.Length * 2);

            foreach (byte hashByte in hash)
                builder.Append(hashByte.ToString("x2"));

            return builder.ToString();
        }
    }
}