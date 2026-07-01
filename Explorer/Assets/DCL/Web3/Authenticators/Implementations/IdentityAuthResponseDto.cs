using DCL.Web3.Chains;
using System;
using System.Collections.Generic;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Response shape of <c>GET {authApiUrl}/identities/{id}</c>. Shared by the launcher auto-login
    ///     (<see cref="TokenFileAuthenticator" />) and the deep-link sign-in flow (<see cref="IdentityByIdFetcher" />).
    /// </summary>
    [Serializable]
    internal struct IdentityAuthResponseDto
    {
        public IdentityDto identity;

        [Serializable]
        public struct IdentityDto
        {
            public string expiration;
            public EphemeralIdentityDto ephemeralIdentity;
            public List<AuthLink> authChain;
        }

        [Serializable]
        public struct EphemeralIdentityDto
        {
            public string address;
            public string privateKey;
            public string publicKey;
        }
    }
}
