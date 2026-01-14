using System;

namespace DCL.Web3.Authenticators
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        public delegate void VerificationDelegate(int code, DateTime expiration, string requestID);

        void SetVerificationListener(VerificationDelegate? callback);

        void CancelCurrentWeb3Operation();
    }
}
