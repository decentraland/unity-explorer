using System;

namespace DCL.Web3Authentication.Signatures
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        public delegate void VerificationDelegate(int code, DateTime expiration);

        void AddVerificationListener(VerificationDelegate callback);
    }
}
