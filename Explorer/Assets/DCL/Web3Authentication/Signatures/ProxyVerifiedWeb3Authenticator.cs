using DCL.Web3Authentication.Identities;

namespace DCL.Web3Authentication.Signatures
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

        public void AddVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate callback) =>
            authenticator.AddVerificationListener(callback);
    }
}
