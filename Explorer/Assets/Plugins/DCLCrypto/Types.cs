using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace DCLCrypto
{
    [Serializable]

    // ReSharper disable InconsistentNaming
    public enum AuthLinkType
    {
        SIGNER,
        ECDSA_EPHEMERAL,
        ECDSA_SIGNED_ENTITY,
        /**
         * See https://github.com/ethereum/EIPs/issues/1654
         */
        ECDSA_EIP_1654_EPHEMERAL,
        /**
         * See https://github.com/ethereum/EIPs/issues/1654
         */
        ECDSA_EIP_1654_SIGNED_ENTITY,
    }

    [Serializable]
    public struct AuthLink
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AuthLinkType type;
        public string payload;
        [CanBeNull] public string signature;
    }

    public class AuthChain : List<AuthLink>
    {
        public AuthChain Clone()
        {
            var newChain = new AuthChain();
            newChain.AddRange(this);
            return newChain;
        }

        public string ToJsonString() =>
            JsonConvert.SerializeObject(this);
    }

    public class AuthIdentity
    {
        public readonly AuthChain AuthChain;
        public readonly Identity EphemeralIdentity;
        public readonly string EthAddress;
        public readonly DateTime Expiration;

        public AuthIdentity(string ethAddress, AuthChain authChain, Identity ephemeralIdentity, DateTime expiration)
        {
            EthAddress = ethAddress;
            AuthChain = authChain;
            EphemeralIdentity = ephemeralIdentity;
            Expiration = expiration;
        }

        public bool IsExpired() =>
            DateTime.Now > Expiration;
    }
}
