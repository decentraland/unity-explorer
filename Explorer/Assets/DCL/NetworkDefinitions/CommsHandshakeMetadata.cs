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
        /// <summary>
        /// Builds the JSON metadata for a comms handshake that requires a secret.
        /// Sends the provided secret as-is (empty string means "no password").
        /// </summary>
        public static string BuildJson(string secret)
        {
            var metadata = new MetadataWithSecret
            {
                intent = INTENT,
                signer = SIGNER,
                isGuest = false,
                secret = secret,
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
