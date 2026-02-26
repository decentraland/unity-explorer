using System;
using UnityEngine;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Shared helper for building the JSON metadata used in the comms handshake.
    /// Centralises the magic strings and struct so that FixedConnectiveRoom,
    /// GateKeeperSceneRoom, and WorldPermissionsService stay in sync.
    /// </summary>
    public static class CommsHandshakeMetadata
    {
        public const string INTENT = "dcl:explorer:comms-handshake";
        public const string SIGNER = "dcl:explorer";
        public const string MISSING_SECRET_FALLBACK = "__missing_secret__";

        /// <summary>
        /// Builds the JSON metadata for a comms handshake that requires a secret.
        /// Uses <see cref="MISSING_SECRET_FALLBACK"/> when the secret is null or empty,
        /// so the handshake proceeds and the server can return 403 instead of 502.
        /// </summary>
        public static string BuildJson(string? secret)
        {
            var metadata = new MetadataWithSecret
            {
                intent = INTENT,
                signer = SIGNER,
                isGuest = false,
                secret = string.IsNullOrEmpty(secret) ? MISSING_SECRET_FALLBACK : secret,
            };

            return JsonUtility.ToJson(metadata);
        }

        [Serializable]
        private struct MetadataWithSecret
        {
            public string intent;
            public string signer;
            public bool isGuest;
            public string secret;
        }
    }
}