using DCL.Web3.Identities;

namespace DCL.Web3.Authenticators
{
    public class ProxyVerifiedWeb3Authenticator : ProxyWeb3Authenticator, IWeb3VerifiedAuthenticator
    {
        private readonly IWeb3VerifiedAuthenticator authenticator;

        public ProxyVerifiedWeb3Authenticator(
            IWeb3VerifiedAuthenticator authenticator,
            IWeb3IdentityCache identityCache)
            : base(authenticator, identityCache)
        {
            this.authenticator = authenticator;
        }

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
            authenticator.SetVerificationListener(callback);
    }
}
