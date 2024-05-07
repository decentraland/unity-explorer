using System;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        public delegate void VerificationDelegate(int code, DateTime expiration);

        void SetVerificationListener(VerificationDelegate? callback);
    }
}
