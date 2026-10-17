using DCL.WebRequests;
using System;
using System.Security.Cryptography;
using System.Text;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Proof-of-possession for the deep link sign-in: the verifier stays in memory and is sent only to the auth
    ///     server when retrieving the identity, while the request id put in the browser URL is derived from its hash.
    ///     Knowing the identity id or the request id is therefore not enough to retrieve the identity.
    /// </summary>
    public static class DeepLinkSignInVerifier
    {
        public const string HEADER = WebRequestHeader.IDENTITY_VERIFIER;

        private const int VERIFIER_BYTES = 32;
        private const int UUID_BYTES = 16;
        private const int UUID_STRING_LENGTH = 36;
        private const string HEX_DIGITS = "0123456789abcdef";

        /// <summary>Base64url (unpadded) encoding of 32 cryptographically random bytes.</summary>
        public static string CreateVerifier()
        {
            var bytes = new byte[VERIFIER_BYTES];

            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        ///     First 16 bytes of SHA-256(verifier) with the UUID v4 version and variant bits set, as a lowercase UUID.
        /// </summary>
        public static string DeriveAuthRequestId(string verifier)
        {
            byte[] hash;

            using (var sha256 = SHA256.Create())
                hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));

            hash[6] = (byte)((hash[6] & 0x0f) | 0x40);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

            // Formatted by hand: Guid(byte[]) would byte-swap the first three groups.
            var sb = new StringBuilder(UUID_STRING_LENGTH);

            for (var i = 0; i < UUID_BYTES; i++)
            {
                if (i is 4 or 6 or 8 or 10)
                    sb.Append('-');

                sb.Append(HEX_DIGITS[hash[i] >> 4]).Append(HEX_DIGITS[hash[i] & 0x0f]);
            }

            return sb.ToString();
        }
    }
}
