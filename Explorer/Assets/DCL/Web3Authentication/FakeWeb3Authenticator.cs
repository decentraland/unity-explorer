using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3Authentication
{
    public class FakeWeb3Authenticator : IWeb3Authenticator
    {
        public async UniTask<IWeb3Identity> Login(CancellationToken cancellationToken)
        {
            var signer = NethereumAccount.CreateRandom();
            var ephemeralAccount = NethereumAccount.CreateRandom();
            DateTime expiration = DateTime.Now.AddMinutes(600);

            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";
            string ephemeralSignature = signer.Sign(ephemeralMessage);

            var authChain = AuthChain.Create();

            authChain.Set(AuthLinkType.SIGNER, new AuthLink
            {
                type = AuthLinkType.SIGNER,
                payload = signer.Address,
                signature = "",
            });

            authChain.Set(AuthLinkType.ECDSA_EPHEMERAL, new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = ephemeralSignature,
            });

            return new DecentralandIdentity(ephemeralAccount, expiration, authChain);
        }

        public async UniTask Logout(CancellationToken cancellationToken) { }
    }
}
