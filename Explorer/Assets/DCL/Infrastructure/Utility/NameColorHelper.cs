using UnityEngine;

namespace DCL.Utilities
{
    public static class NameColorHelper
    {
        private static readonly Color DEFAULT_COLOR = Color.white;
        private static readonly float DEFAULT_SATURATION = .75f;
        private static readonly float DEFAULT_VALUE = 1f;

        /// <summary>
        /// Generates a deterministic Unity <see cref="Color"/> for a given username.
        /// </summary>
        /// <returns>
        /// A <see cref="Color"/> object derived deterministically from the username.
        /// If the username is null or empty, <see cref="DEFAULT_COLOR"/> (white) is returned.
        /// </returns>
        public static Color GetNameColor(string? username)
        {
            if (string.IsNullOrEmpty(username))
                return DEFAULT_COLOR;

            uint hash = GetStableHashFNV1a(username);

            float hue = (float)hash / uint.MaxValue;

            return Color.HSVToRGB(hue, DEFAULT_SATURATION, DEFAULT_VALUE);
        }

        /// <summary>
        /// Computes a deterministic, platform-agnostic 32-bit hash for a string using the FNV-1a algorithm.
        /// The hash is well-spread, small changes in the string produce large changes in the hash.
        /// </summary>
        /// <returns>
        /// A <see cref="uint"/> representing the stable hash of the input string.
        /// </returns>
        private static uint GetStableHashFNV1a(string s)
        {
            const uint FNV_OFFSET_BASIS = 2166136261u;
            const uint FNV_PRIME = 16777619u;

            uint hash = FNV_OFFSET_BASIS;

            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= FNV_PRIME;
            }

            return hash;
        }
    }
}
