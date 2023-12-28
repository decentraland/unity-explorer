using System;

namespace DCL.Web3Authentication
{
    public interface IWeb3VerifiedAuthenticator : IWeb3Authenticator
    {
        void AddVerificationListener(Action<int> callback);
    }
}
