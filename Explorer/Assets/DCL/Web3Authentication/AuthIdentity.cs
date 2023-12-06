using System;

namespace DCL.Web3Authentication
{
    [Obsolete("This class is confusing. Could be probably replaced by DecentralandEntityPayloadSigningProtocol")]
    public class AuthIdentity
    {
        public readonly AuthChain AuthChain;
        public readonly IWeb3Identity EphemeralIdentity;
        public readonly string EthAddress;
        public readonly DateTime Expiration;

        public AuthIdentity(string ethAddress, AuthChain authChain, IWeb3Identity ephemeralIdentity, DateTime expiration)
        {
            EthAddress = ethAddress;
            AuthChain = authChain;
            EphemeralIdentity = ephemeralIdentity;
            Expiration = expiration;
        }

        public bool IsExpired => DateTime.Now > Expiration;
    }
}
