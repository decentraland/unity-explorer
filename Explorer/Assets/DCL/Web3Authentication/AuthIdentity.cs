using System;

namespace DCL.Web3Authentication
{
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

        public bool IsExpired => DateTime.Now > Expiration;
    }
}
